using System.Net.Http.Headers;
using Windows.Foundation;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;


public class BgmOAuthService : IBgmOAuthService
{
    private readonly ILocalSettingsService _localSettingsService;
    private BgmAccount _bgmAccount;
    private DateTime _lastUpdateDateTime;
    private readonly TimeSpan _minUpdateTime = new(1, 0, 0, 0);
    private readonly TimeSpan _minRefreshTime = new(5, 0, 0, 0);
    private bool _isInitialized;
    
    public BgmOAuthService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _bgmAccount = new BgmAccount();
        _lastUpdateDateTime = DateTime.UnixEpoch;
    }

    private async Task Init()
    {
        _isInitialized = true;
        _bgmAccount = await _localSettingsService.ReadSettingAsync<BgmAccount?>(KeyValues.BangumiOAuthState) ?? new BgmAccount();
        //todo:兼容直接获取token的方式
        _lastUpdateDateTime = await _localSettingsService.ReadSettingAsync<DateTime?>(KeyValues.BangumiOAuthStateLastUpdate) ?? DateTime.UnixEpoch;
    }

    public async Task StartOAuth()
    {
        await Launcher.LaunchUriAsync(new Uri(BgmOAuthConfig.OAuthUrl));
    }

    /// <summary>
    /// 使用回调的uri（包含code）完成OAuth
    /// </summary>
    public async Task FinishOAuthWithUri(Uri uri)
    {
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            App.MainWindow.Activate(); //把窗口提到最前面
        });
        WwwFormUrlDecoder decoder = new(uri.Query);
        var code = decoder.GetFirstValueByNameOrEmpty("code");
        HttpClient client = GetHttpClient();
        try
        {
            HttpResponseMessage response = await client.GetAsync(string.Format(BgmOAuthConfig.GetTokenUrl, code));
            if (!response.IsSuccessStatusCode) return;
            JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
            _bgmAccount.BangumiAccessToken = json["access_token"]!.ToString();
            _bgmAccount.BangumiRefreshToken = json["refresh_token"]!.ToString();
            await GetBgmAccount();
        }
        catch
        {
            //todo: 报错提示
        }
    }
    
    /// <summary>
    /// 使用更新token获取新的token，并刷新缓存
    /// </summary>
    /// <returns>是否成功</returns>
    public async Task<bool> RefreshOAuthState()
    {
        if (!_isInitialized) await Init();
        if (string.IsNullOrEmpty(_bgmAccount.BangumiRefreshToken)) return false;
        HttpClient client = GetHttpClient();
        try
        {
            HttpResponseMessage response = await client.GetAsync(string.Format(BgmOAuthConfig.RefreshTokenUrl, _bgmAccount.BangumiRefreshToken));
            JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
            _bgmAccount.BangumiAccessToken = json["access_token"]!.ToString();
            _bgmAccount.BangumiRefreshToken = json["refresh_token"]!.ToString();
            await GetBgmAccount();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 用于获取用户账户，默认从缓存读取
    /// </summary>
    /// <param name="forceRefresh">强制更新</param>
    /// <returns></returns>
    public async Task<BgmAccount> GetBgmAccountWithCache(bool forceRefresh=false)
    {
        if (!_isInitialized) await Init();
        if (DateTime.Now - _lastUpdateDateTime >= _minUpdateTime || forceRefresh)
            await GetBgmAccount();
        return _bgmAccount;
    }
    
    /// <summary>
    /// 获取Bgm账户，并刷新缓存 <br/>
    /// </summary>
    private async Task GetBgmAccount()
    {
        if (!_isInitialized) await Init();
        if (!_bgmAccount.OAuthed) return;
        HttpClient httpClient = GetHttpClient();
        try
        {
            //获取token状态与用户id
            Dictionary<string, string> parameters = new() { { "access_token", _bgmAccount.BangumiAccessToken } };
            HttpResponseMessage responseMessage = await httpClient.PostAsync("https://bgm.tv/oauth/token_status", 
                new FormUrlEncodedContent(parameters));
            if (!responseMessage.IsSuccessStatusCode) return;
            JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
            if (!int.TryParse(json["expires"]!.ToString(), out var expires)) return;
            _bgmAccount.UserId = json["user_id"]!.ToString();
            _bgmAccount.Expires = IBgmOAuthService.UnixTimeStampToDateTime(expires);
            _lastUpdateDateTime = DateTime.Now;
            await SaveOAuthState();
            await SaveLastUpdateTime();
            //下载用户数据
            //用户名
            responseMessage = await httpClient.GetAsync($"https://api.bgm.tv/v0/users/{_bgmAccount.UserId}");
            JToken userJson = JToken.Parse(await responseMessage.Content.ReadAsStringAsync());
            _bgmAccount.Name = userJson["nickname"]!.ToString();
            await SaveOAuthState();
            //头像
            var avatarUrl = userJson["avatar"]!["large"]!.ToString();
            avatarUrl = avatarUrl[..avatarUrl.LastIndexOf('?')]; //xx.jpg?r=1684973055&hd=1 => xx.jpg
            var path = await DownloadHelper.DownloadAndSaveImageAsync(avatarUrl);
            if (path != null)
                _bgmAccount.Avatar = path;
            await SaveOAuthState();
        }
        catch
        {
            //
        }
    }

    public async Task<string> GetOAuthStateString(bool forceRefresh=false)
    {
        if (!_isInitialized) await Init();
        if (!_bgmAccount.OAuthed) return "BgmOAuthService_NoLogin".GetLocalized();
        BgmAccount bgmOAuthState = await GetBgmAccountWithCache(forceRefresh);
        if (!bgmOAuthState.OAuthed) return "BgmOAuthService_NoLogin".GetLocalized();
        return "BgmOAuthService_Id".GetLocalized() + bgmOAuthState.UserId + "BgmOAuthService_AuthLimit".GetLocalized() +
               bgmOAuthState.Expires.ToShortDateString();
    }
    
    public async Task<bool> QuitLoginBgm()
    {
        if (!_isInitialized) await Init();
        if (!_bgmAccount.OAuthed) return false;
        _bgmAccount = new BgmAccount();
        await SaveOAuthState();
        return true;
    }
    
    public async Task TryRefreshOAuthAsync()
    {
        if (!_isInitialized) await Init();
        if (await CheckForRefresh() == false) return;
        await RefreshOAuthState();
    }

    /// <summary>
    /// 检查是否需要刷新授权
    /// </summary>
    private async Task<bool> CheckForRefresh()
    {
        if (!_isInitialized)
            await Init();
        if (!_bgmAccount.OAuthed) return false;
        return _bgmAccount.Expires - DateTime.Now < _minRefreshTime;
    }

    private async Task SaveOAuthState()
    {
        await UiThreadInvokeHelper.InvokeAsync(async Task() =>
        {
            OnOAuthStateChange?.Invoke(_bgmAccount);
            await _localSettingsService.SaveSettingAsync(KeyValues.BangumiOAuthState, _bgmAccount);
        });
    }

    private async Task SaveLastUpdateTime()
    {
        await UiThreadInvokeHelper.InvokeAsync(async Task () =>
        {
            await _localSettingsService.SaveSettingAsync(KeyValues.BangumiOAuthStateLastUpdate, _lastUpdateDateTime);
        });
    }

    public event IBgmOAuthService.Delegate? OnOAuthStateChange;
    
    
    private static HttpClient GetHttpClient()
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "GoldenPotato/GalgameManager/1.0-dev (Windows) (https://github.com/GoldenPotato137/GalgameManager)");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}