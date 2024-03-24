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
    private readonly TimeSpan _minRefreshTime = new(2, 0, 0, 0);
    private bool _isInitialized;
    
    public event Action<BgmOAuthStatus>? OnAuthResultChange;

    
    public BgmOAuthService(ILocalSettingsService localSettingsService, IInfoService infoService, IConfiguration config)
    {
        _localSettingsService = localSettingsService;
        _infoService = infoService;
        _config = config;
    }

    public async Task Init()
    {
        _isInitialized = true;
        await Upgrade();
        
        BgmAccount? account = await _localSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount);
        if (account is not null && DateTime.Now >= account.NextRefresh)
            _ = Task.Run(RefreshAccountAsync);
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

        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.OAuthUpgraded2) == false)
        {
            BgmAccount? account = await _localSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiOAuthState);
            await _localSettingsService.RemoveSettingAsync(KeyValues.BangumiOAuthState);
            if (account?.OAuthed is true)
                await _localSettingsService.SaveSettingAsync(KeyValues.BangumiAccount, account);
            await _localSettingsService.SaveSettingAsync(KeyValues.OAuthUpgraded2, true);
        }
    }

    public async Task StartOAuthAsync()
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
            OnAuthResultChange?.Invoke(BgmOAuthStatus.FetchingToken);
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
            BgmAccount account = new()
            {
                BangumiAccessToken = json["token"]!.ToString(),
                BangumiRefreshToken = json["refreshToken"]!.ToString()
            };
            await _localSettingsService.SaveSettingAsync(KeyValues.BangumiAccount, account);
            await GetBgmAccount(OnAuthResultChange);
        }
        catch (Exception e)
        {
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                OnAuthResultChange?.Invoke(BgmOAuthStatus.Failed);
                _infoService.Event(EventType.BgmOAuthEvent, InfoBarSeverity.Error,
                    "BgmOAuthService_FetchTokenFailed".GetLocalized(), e);
            });
        }
    }

    public async Task AuthWithAccessToken(string accessToken)
    {
        if (!_isInitialized) await Init();
        BgmAccount account = await _localSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount) ?? new();
        account.BangumiAccessToken = accessToken;
        account.BangumiRefreshToken = string.Empty;
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiAccount, account);
        var result = await GetBgmAccount();
        if (result == false) await _localSettingsService.RemoveSettingAsync(KeyValues.BangumiAccount);
    }
    
    public async Task<bool> RefreshAccountAsync()
    {
        BgmAccount? account = await _localSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount);
        if (string.IsNullOrEmpty(account?.BangumiRefreshToken)) return false;
        HttpClient client = GetHttpClient();
        try
        {
            HttpResponseMessage response = await client.GetAsync(new Uri(await BaseUriAsync(), 
                "bangumi/refresh").AddQuery("refreshToken", account.BangumiRefreshToken));
            if (response.IsSuccessStatusCode == false)
                throw new Exception(await response.Content.ReadAsStringAsync());
            JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
            account.BangumiAccessToken = json["token"]!.ToString();
            account.BangumiRefreshToken = json["refreshToken"]!.ToString();
            await _localSettingsService.SaveSettingAsync(KeyValues.BangumiAccount, account);
            _ = Task.Run(() => GetBgmAccount(OnAuthResultChange)); //异步获取用户信息
            return true;
        }
        catch(Exception e)
        {
            _infoService.Event(EventType.BgmOAuthEvent, e is HttpRequestException ? InfoBarSeverity.Warning 
                    :InfoBarSeverity.Error, "BgmOAuthService_RefreshFailed".GetLocalized(), e);
            return false;
        }
    }
    
    /// <summary>
    /// 获取Bgm账户信息并保存至local setting，若account不存在则什么都不做
    /// </summary>
    private async Task<bool> GetBgmAccount(Action<BgmOAuthStatus>? onStatusChanged = null)
    {
        BgmAccount? account = await _localSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount);
        if (account is null) return false;
        HttpClient httpClient = GetHttpClient();
        try
        {
            //获取token状态与用户id
            Dictionary<string, string> parameters = new() { { "access_token", account.BangumiAccessToken } };
            await UiThreadInvokeHelper.InvokeAsync(() => { onStatusChanged?.Invoke(BgmOAuthStatus.FetchingTokenInfo); });
            HttpResponseMessage responseMessage =
                await httpClient.PostAsync("https://bgm.tv/oauth/token_status", new FormUrlEncodedContent(parameters));
            if (!responseMessage.IsSuccessStatusCode)
            {
                var content = await responseMessage.Content.ReadAsStringAsync();
                _infoService.Event(EventType.BgmOAuthEvent, InfoBarSeverity.Error,
                    "BgmOAuthService_FetchTokenInfoFailed".GetLocalized(), msg: content);
                return false;
            }
            JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
            if (!int.TryParse(json["expires"]!.ToString(), out var expires))
                throw new Exception("No expires in response."); //不应该发生，没有expires字段
            account.UserId = json["user_id"]?.ToString() ?? "-1";
            account.Expires = IBgmOAuthService.UnixTimeStampToDateTime(expires);
            account.NextRefresh = string.IsNullOrEmpty(account.BangumiRefreshToken)
                ? new DateTime(2077, 11, 4) : DateTime.Now + _minRefreshTime;
            //下载用户数据
            await UiThreadInvokeHelper.InvokeAsync(() => { onStatusChanged?.Invoke(BgmOAuthStatus.FetchingAccount); });
            //用户名
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + account.BangumiAccessToken);
            responseMessage = await httpClient.GetAsync($"https://api.bgm.tv/v0/me");
            JToken userJson = JToken.Parse(await responseMessage.Content.ReadAsStringAsync());
            account.Name = userJson["nickname"]?.ToString() ?? userJson["username"]?.ToString() ?? "BgmAccount_NoName".GetLocalized();
            //头像
            await UiThreadInvokeHelper.InvokeAsync(() => { onStatusChanged?.Invoke(BgmOAuthStatus.FetchingAccountImage); });
            if (userJson["avatar"]?["large"] != null)
            {
                var avatarUrl = userJson["avatar"]!["large"]!.ToString();
                var path = await DownloadHelper.DownloadAndSaveImageAsync(avatarUrl,
                    fileNameWithoutExtension: "bgmAvatar", onException: (e) =>
                    {
                        _infoService.Event(EventType.BgmOAuthEvent, InfoBarSeverity.Warning,
                            "BgmOAuthService_FetchAvatarFailed".GetLocalized(), e);
                    });
                account.Avatar = path ?? string.Empty;
            }

            await UiThreadInvokeHelper.InvokeAsync(() => { onStatusChanged?.Invoke(BgmOAuthStatus.Done); });
            await _localSettingsService.SaveSettingAsync(KeyValues.BangumiAccount, account);
            return true;
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.BgmOAuthEvent, e is HttpRequestException ? InfoBarSeverity.Warning 
                    : InfoBarSeverity.Error, "BgmOAuthService_FetchAccountFailed".GetLocalized(), e);
            await UiThreadInvokeHelper.InvokeAsync(() => { onStatusChanged?.Invoke(BgmOAuthStatus.Failed); });
            return false;
        }
    }
    
    public async Task LogoutAsync()
    {
        await _localSettingsService.RemoveSettingAsync(KeyValues.BangumiAccount);
    }

    private async Task<Uri> BaseUriAsync()
    {
        _pvnService ??= App.GetService<IPvnService>();
        PvnServerInfo? serverInfo = await _pvnService.GetServerInfoAsync();
        if (serverInfo?.BangumiOauth2Enable == true) return _pvnService.BaseUri;
        return new Uri(_config["PotatoVNOfficialServer"]!);
    }

    private static HttpClient GetHttpClient()
    {
        return Utils.GetDefaultHttpClient().WithApplicationJson();
    }
}