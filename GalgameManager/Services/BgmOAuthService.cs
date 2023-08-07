using System.Net.Http.Headers;
using Windows.Foundation;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;


public class BgmOAuthService : IBgmOAuthService
{
    private readonly ILocalSettingsService _localSettingsService;
    private BgmOAuthState _bgmOAuthState;
    private DateTime _lastUpdateDateTime;
    private readonly TimeSpan _minUpdateTime = new(1, 0, 0, 0);
    private readonly TimeSpan _minRefreshTime = new(5, 0, 0, 0);
    private bool _isInitialized;
    
    public BgmOAuthService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _bgmOAuthState = new BgmOAuthState();
        _lastUpdateDateTime = DateTime.UnixEpoch;
    }

    private async Task Init()
    {
        _isInitialized = true;
        _bgmOAuthState = await _localSettingsService.ReadSettingAsync<BgmOAuthState?>(KeyValues.BangumiOAuthState) ?? new BgmOAuthState();
        _lastUpdateDateTime = await _localSettingsService.ReadSettingAsync<DateTime?>(KeyValues.BangumiOAuthStateLastUpdate) ?? DateTime.UnixEpoch;
    }

    public async Task StartOAuth()
    {
        await Launcher.LaunchUriAsync(new Uri(BgmOAuthConfig.OAuthUrl));
    }

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
            _bgmOAuthState.BangumiAccessToken = json["access_token"]!.ToString();
            _bgmOAuthState.BangumiRefreshToken = json["refresh_token"]!.ToString();
            await GetOAuthStateFromBgm();
        }
        catch
        {
            //todo: 报错提示
        }
    }

    /// <summary>
    /// 获取Bgm OAuth状态，并刷新缓存
    /// </summary>
    /// <returns>OAuth状态</returns>
    private async Task<BgmOAuthState?> GetOAuthStateFromBgm()
    {
        if (!_isInitialized) await Init();
        if (!_bgmOAuthState.OAuthed) return null;
        HttpClient httpClient = GetHttpClient();
        Dictionary<string, string> parameters = new() { { "access_token", _bgmOAuthState.BangumiAccessToken } };
        HttpResponseMessage responseMessage = await httpClient.PostAsync("https://bgm.tv/oauth/token_status", 
            new FormUrlEncodedContent(parameters));
        if (!responseMessage.IsSuccessStatusCode) return null;
        JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
        if (!int.TryParse(json["expires"]!.ToString(), out var expires)) return null;
        _bgmOAuthState.UserId = json["user_id"]!.ToString();
        _bgmOAuthState.Expires = IBgmOAuthService.UnixTimeStampToDateTime(expires);
        _lastUpdateDateTime = DateTime.Now;
        await SaveOAuthState();
        await SaveLastUpdateTime();
        return _bgmOAuthState;
    }

    /// <summary>
    /// 更新授权状态，并刷新授权时间
    /// </summary>
    /// <returns>是否成功</returns>
    public async Task<bool> RefreshOAuthState()
    {
        if (!_isInitialized) await Init();
        if (string.IsNullOrEmpty(_bgmOAuthState.BangumiRefreshToken)) return false;
        HttpClient client = GetHttpClient();
        try
        {
            HttpResponseMessage response = await client.GetAsync(string.Format(BgmOAuthConfig.RefreshTokenUrl, _bgmOAuthState.BangumiRefreshToken));
            JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
            _bgmOAuthState.BangumiAccessToken = json["access_token"]!.ToString();
            _bgmOAuthState.BangumiRefreshToken = json["refresh_token"]!.ToString();
            await GetOAuthStateFromBgm();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetOAuthStateString(bool forceRefresh=false)
    {
        if (!_isInitialized) await Init();
        if (!_bgmOAuthState.OAuthed) return "";
        BgmOAuthState? bgmOAuthState = await GetOAuthState(forceRefresh);
        if (bgmOAuthState is null || !bgmOAuthState.OAuthed) return "";
        return  bgmOAuthState.OAuthed ? "用户ID:" + bgmOAuthState.UserId + ", 授权至" + bgmOAuthState.Expires.ToShortDateString() : "" ;
    }

    /// <summary>
    /// 用于获取OAuth状态，默认从缓存读取
    /// </summary>
    /// <param name="forceRefresh">强制更新，同时刷新缓存</param>
    /// <returns></returns>
    public async Task<BgmOAuthState?> GetOAuthState(bool forceRefresh=false)
    {
        if (!_isInitialized) await Init();
        if (!(DateTime.Now - _lastUpdateDateTime < _minUpdateTime))
        {
            await GetOAuthStateFromBgm();
        }

        if (!await CheckForRefresh()) return _bgmOAuthState;
        await RefreshOAuthState();
        return _bgmOAuthState;
    }

    public async Task<bool> QuitLoginBgm()
    {
        if (!_isInitialized) await Init();
        if (!_bgmOAuthState.OAuthed) return false;
        _bgmOAuthState = new BgmOAuthState();
        await SaveOAuthState();
        return true;
    }

    /// <summary>
    /// 检查是否需要刷新授权
    /// </summary>
    private async Task<bool> CheckForRefresh()
    {
        if (!_isInitialized)
            await Init();
        if (!_bgmOAuthState.OAuthed) return false;
        return _bgmOAuthState.Expires - DateTime.Now < _minRefreshTime;
    }

    private async Task SaveOAuthState()
    {
        await UiThreadInvokeHelper.InvokeAsync(async Task() =>
        {
            OnOAuthStateChange?.Invoke(_bgmOAuthState);
            await _localSettingsService.SaveSettingAsync(KeyValues.BangumiOAuthState, _bgmOAuthState);
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