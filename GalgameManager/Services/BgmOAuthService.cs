using System.Net.Http.Headers;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
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

    public async Task FinishOAuthWithUri(string uri)
    {
        if (uri.StartsWith(BgmOAuthConfig.RedirectUri))
        {
            await FinishOAuthWithCode(uri.Split("=")[1]);
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
    /// 用于与 https://bgm.tv/oauth/access_token 交互，更新授权状态，并刷新授权时间
    /// </summary>
    public async Task<bool> RefreshOAuthState()
    {
        if (!_isInitialized) await Init();
        if (!_bgmOAuthState.OAuthed) return false;
        HttpClient httpClient = GetHttpClient();
        Dictionary<string, string> parameters = new()
        {
            { "grant_type", "authorization_code" },
            { "client_id", BgmOAuthConfig.AppId },
            { "client_secret", BgmOAuthConfig.AppSecret },
            { "redirect_uri", BgmOAuthConfig.RedirectUri },
            { "refresh_token", _bgmOAuthState.BangumiRefreshToken }
        };
        FormUrlEncodedContent requestContent = new(parameters);
        HttpResponseMessage responseMessage = await httpClient.PostAsync("https://bgm.tv/oauth/access_token", requestContent);
        if (!responseMessage.IsSuccessStatusCode) return false;
        JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
        _bgmOAuthState.BangumiAccessToken = json["access_token"]!.ToString();
        _bgmOAuthState.BangumiRefreshToken = json["refresh_token"]!.ToString();
        await GetOAuthStateFromBgm();
        return true;
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

    private async Task<bool> CheckForRefresh()
    {
        if (!_isInitialized)
            await Init();
        if (!_bgmOAuthState.OAuthed) return false;
        return _bgmOAuthState.Expires - DateTime.Now < _minRefreshTime;
    }

    private async Task SaveOAuthState()
    {
        OnOAuthStateChange?.Invoke(_bgmOAuthState);
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiOAuthState, _bgmOAuthState);
    }

    private async Task SaveLastUpdateTime()
    {
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiOAuthStateLastUpdate, _lastUpdateDateTime);
    }

    public event IBgmOAuthService.Delegate? OnOAuthStateChange;

    public async Task<bool> FinishOAuthWithCode(string code)
    {
        if (!_isInitialized) await Init();
        HttpClient httpClient = GetHttpClient();
        Dictionary<string, string> parameters = new()
        {
            { "grant_type", "authorization_code" },
            { "client_id", BgmOAuthConfig.AppId },
            { "client_secret", BgmOAuthConfig.AppSecret },
            { "redirect_uri", BgmOAuthConfig.RedirectUri },
            { "code", code }
        };
        FormUrlEncodedContent requestContent = new(parameters);
        HttpResponseMessage responseMessage = httpClient.PostAsync("https://bgm.tv/oauth/access_token", requestContent).Result;
        if (!responseMessage.IsSuccessStatusCode) return false ;
        JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
        _bgmOAuthState.BangumiAccessToken = json["access_token"]!.ToString();
        _bgmOAuthState.BangumiRefreshToken = json["refresh_token"]!.ToString();
        await GetOAuthStateFromBgm();
        return true;
    }

    
    private static HttpClient GetHttpClient()
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "GoldenPotato/GalgameManager/1.0-dev (Windows) (https://github.com/GoldenPotato137/GalgameManager)");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}