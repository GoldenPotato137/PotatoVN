using Windows.Foundation;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;


public class BgmOAuthService : IBgmOAuthService
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInfoService _infoService;
    private IPvnService? _pvnService;
    private readonly IConfiguration _config;
    private BgmAccount _bgmAccount;
    private DateTime _lastUpdateDateTime;
    private readonly TimeSpan _minUpdateTime = new(1, 0, 0, 0);
    private readonly TimeSpan _minRefreshTime = new(5, 0, 0, 0);
    private bool _isInitialized;
    
    public event IBgmOAuthService.Delegate? OnOAuthStateChange;
    public event Action<OAuthResult, string>? OnAuthResultChange;

    
    public BgmOAuthService(ILocalSettingsService localSettingsService, IInfoService infoService, IConfiguration config)
    {
        _localSettingsService = localSettingsService;
        _infoService = infoService;
        _config = config;
        _bgmAccount = new BgmAccount();
        _lastUpdateDateTime = DateTime.UnixEpoch;
    }

    private async Task Init()
    {
        _isInitialized = true;
        await Upgrade();
        
        _bgmAccount = await _localSettingsService.ReadSettingAsync<BgmAccount?>(KeyValues.BangumiOAuthState) ?? new BgmAccount();
        _lastUpdateDateTime = await _localSettingsService.ReadSettingAsync<DateTime?>(KeyValues.BangumiOAuthStateLastUpdate) ?? DateTime.UnixEpoch;
    }

    /// <summary>
    /// 升级旧有设置
    /// </summary>
    private async Task Upgrade()
    {
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.OAuthUpgraded) == false)
        {
            var bangumiToken = await _localSettingsService.ReadSettingAsync<string>(KeyValues.BangumiToken);
            if (!string.IsNullOrEmpty(bangumiToken))
            {
                await _localSettingsService.SaveSettingAsync(KeyValues.BangumiOAuthState, new BgmAccount
                {
                    BangumiAccessToken = bangumiToken
                });
                // 升级后强制更新账号状态
                await _localSettingsService.SaveSettingAsync(KeyValues.BangumiOAuthStateLastUpdate, DateTime.UnixEpoch);
            }
            await _localSettingsService.SaveSettingAsync(KeyValues.OAuthUpgraded, true);
        }
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
            App.SetWindowMode(WindowMode.Normal);
            OnAuthResultChange?.Invoke(OAuthResult.FetchingToken, OAuthResult.FetchingToken.ToMsg());
        });
        WwwFormUrlDecoder decoder = new(uri.Query);
        var code = decoder.GetFirstValueByNameOrEmpty("code");
        HttpClient client = GetHttpClient();
        try
        {
            HttpResponseMessage response = await client.GetAsync(new Uri(await BaseUriAsync(), 
                "bangumi/oauth").AddQuery("code", code));
            if (!response.IsSuccessStatusCode) return;
            JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
            _bgmAccount.BangumiAccessToken = json["token"]!.ToString();
            _bgmAccount.BangumiRefreshToken = json["refreshToken"]!.ToString();
            await GetBgmAccount();
        }
        catch (Exception e)
        {
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                OnAuthResultChange?.Invoke(OAuthResult.Failed, OAuthResult.Failed.ToMsg()+e.Message);
                _infoService.Event(EventType.BgmOAuthEvent, InfoBarSeverity.Error, OAuthResult.Failed.ToMsg(), e);
            });
        }
    }

    public async Task AuthWithAccessToken(string accessToken)
    {
        if (!_isInitialized) await Init();
        _bgmAccount.BangumiAccessToken = accessToken;
        _bgmAccount.BangumiRefreshToken = string.Empty;
        await GetBgmAccount();
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
            HttpResponseMessage response = await client.GetAsync(new Uri(await BaseUriAsync(), 
                "bangumi/refresh").AddQuery("refreshToken", _bgmAccount.BangumiRefreshToken));
            JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
            _bgmAccount.BangumiAccessToken = json["token"]!.ToString();
            _bgmAccount.BangumiRefreshToken = json["refreshToken"]!.ToString();
            await GetBgmAccount();
            return true;
        }
        catch(Exception e)
        {
            _infoService.Event(EventType.BgmOAuthEvent, InfoBarSeverity.Error,
                "BgmOAuthService_RefreshFailed".GetLocalized(), e);
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
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            OnAuthResultChange?.Invoke(OAuthResult.FetchingAccount, OAuthResult.FetchingAccount.ToMsg());
        });
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
            _bgmAccount.UserId = json["user_id"]?.ToString() ?? "-1";
            _bgmAccount.Expires = IBgmOAuthService.UnixTimeStampToDateTime(expires);
            _lastUpdateDateTime = DateTime.Now;
            //下载用户数据
            //用户名
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _bgmAccount.BangumiAccessToken);
            responseMessage = await httpClient.GetAsync($"https://api.bgm.tv/v0/me");
            JToken userJson = JToken.Parse(await responseMessage.Content.ReadAsStringAsync());
            _bgmAccount.Name = userJson["nickname"]?.ToString() ?? userJson["username"]?.ToString() ?? "BgmAccount_NoName".GetLocalized();
            //头像
            if (userJson["avatar"]?["large"] != null)
            {
                var avatarUrl = userJson["avatar"]!["large"]!.ToString();
                if (avatarUrl.Contains('?'))
                    avatarUrl = avatarUrl[..avatarUrl.LastIndexOf('?')]; //xx.jpg?r=1684973055&hd=1 => xx.jpg
                var path = await DownloadHelper.DownloadAndSaveImageAsync(avatarUrl);
                if (path != null)
                    _bgmAccount.Avatar = path;
            }

            await SaveLastUpdateTime();
            await SaveOAuthState();
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                OnAuthResultChange?.Invoke(OAuthResult.Done, OAuthResult.Done.ToMsg());
            });
        }
        catch (Exception e)
        {
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                OnAuthResultChange?.Invoke(OAuthResult.Failed, OAuthResult.Failed.ToMsg() + e.Message);
            });
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

    private async Task<Uri> BaseUriAsync()
    {
        if (_pvnService is null) _pvnService = App.GetService<IPvnService>();
        PvnServerInfo? serverInfo = await _pvnService.GetServerInfoAsync();
        if (serverInfo?.BangumiOauth2Enable == true) return _pvnService.BaseUri;
        return new Uri(_config["PotatoVNOfficialServer"]!);
    }

    private static HttpClient GetHttpClient()
    {
        return Utils.GetDefaultHttpClient().WithApplicationJson();
    }
}