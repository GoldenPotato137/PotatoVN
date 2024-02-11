using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;

public class PvnService : IPvnService
{
    private readonly ILocalSettingsService _settingsService;
    private readonly IBgmOAuthService _bgmService;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    public Uri BaseUri { get; private set; }
    public Action<PvnServiceStatus>? StatusChanged { get; set; }

    private PvnServerInfo? _serverInfo;

    public PvnService(ILocalSettingsService settingsService, IConfiguration config, IBgmOAuthService bgmService)
    {
        _settingsService = settingsService;
        _bgmService = bgmService;
        _config = config;
        BaseUri = GetBaseUri();
        _httpClient = Utils.GetDefaultHttpClient();

        _settingsService.OnSettingChanged += (key, _) =>
        {
            if (key is KeyValues.PvnServerType or KeyValues.PvnServerEndpoint)
            {
                BaseUri = GetBaseUri();
                _serverInfo = null;
            }
        };
    }

    public async Task<PvnServerInfo?> GetServerInfoAsync()
    {
        if (_serverInfo is null)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(new Uri(BaseUri, "server/info"));
            response.EnsureSuccessStatusCode();
            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            _serverInfo = new PvnServerInfo
            {
                BangumiOauth2Enable = jsonToken["bangumiOAuth2Enable"]!.Value<bool>(),
                DefaultLoginEnable = jsonToken["defaultLoginEnable"]!.Value<bool>(),
                BangumiLoginEnable = jsonToken["bangumiLoginEnable"]!.Value<bool>()
            };
        }
        
        return _serverInfo;
    }

    public async Task<PvnAccount?> LoginAsync(string username, string password)
    {
        Dictionary<string, string> payload = new()
        {
            { "userName", username },
            { "passWord", password }
        };
        HttpResponseMessage response = await _httpClient.PostAsync(new Uri(BaseUri, "user/session"), 
            payload.ToJsonContent());
        if(response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException("PvnService_PasswordIncorrect".GetLocalized());
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException("PvnService_ServerUnavailable".GetLocalized());
        response.EnsureSuccessStatusCode();
        return await ResolveAccount(response, PvnAccount.LoginMethodEnum.Default);
    }
    
    public async Task<PvnAccount?> RegisterAsync(string username, string password)
    {
        Dictionary<string, string> payload = new()
        {
            { "userName", username },
            { "passWord", password }
        };
        HttpResponseMessage response = await _httpClient.PostAsync(new Uri(BaseUri, "user"), 
            payload.ToJsonContent());
        if(response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException("PvnService_UserNameAlreadyTaken".GetLocalized());
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException("PvnService_ServerUnavailable".GetLocalized());
        response.EnsureSuccessStatusCode();
        return await ResolveAccount(response, PvnAccount.LoginMethodEnum.Default);
    }

    public async Task<PvnAccount?> LoginViaBangumiAsync()
    {
        BgmAccount? bgmAccount = await _settingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiOAuthState);
        Task getBgmAccountTask = Task.CompletedTask;
        if (bgmAccount is null || bgmAccount.OAuthed == false)
        {
            await _bgmService.StartOAuth();
            getBgmAccountTask = Task.Run(async () =>
            {
                for(var i = 0; i <= 60 * 5; i++)  //timeout: 60sec
                {
                    await Task.Delay(200);
                    bgmAccount = await _settingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiOAuthState);
                    if (bgmAccount is not null && bgmAccount.OAuthed) break;
                }
            });
        }
        await getBgmAccountTask;
        if (bgmAccount is null)
            throw new AuthenticationException("PvnService_NoBgmAccount".GetLocalized());

        Dictionary<string, string> payload = new()
        {
            {"bgmToken", bgmAccount.BangumiAccessToken}
        };
        HttpResponseMessage response = await _httpClient.PostAsync(new Uri(BaseUri, "user/session/bgm"), 
            payload.ToJsonContent());
        if(response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException("Bangumi token invalid."); //不应该发生
        if (response.StatusCode == HttpStatusCode.BadGateway)
            throw new InvalidOperationException("PvnService_ServerCannotConnectBgm".GetLocalized());
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException("PvnService_ServerUnavailable".GetLocalized());
        response.EnsureSuccessStatusCode();
        return await ResolveAccount(response, PvnAccount.LoginMethodEnum.Bangumi);
    }

    public async Task<PvnAccount?> ModifyAccountAsync(string? userDisplayName, string? avatarPath, string? newPassword,
        string? oldPassword)
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        HttpClient client = Utils.GetDefaultHttpClient().AddToken(account.Token);
        if (avatarPath is not null)
        {
            StatusChanged?.Invoke(PvnServiceStatus.UploadingAvatar);
            HttpResponseMessage tmp = await client.GetAsync(
                new Uri(BaseUri, "oss/put").AddQuery("objectFullName", $"avatar{Path.GetExtension(avatarPath)}"));
            if (tmp.IsSuccessStatusCode == false)
                throw new Exception($"Get Oss presigned url failed with code {tmp.StatusCode}.");
            var presignedUrl = await tmp.Content.ReadAsStringAsync();
            await UploadFileAsync(presignedUrl, avatarPath);
        }

        Dictionary<string, string> payload = new();
        if(string.IsNullOrEmpty(userDisplayName) == false)
            payload["userDisplayName"] = userDisplayName;
        if(string.IsNullOrEmpty(newPassword) == false)
        {
            payload["newPassWord"] = newPassword;
            payload["oldPassWord"] = oldPassword ?? throw new ArgumentNullException(nameof(oldPassword));
        }
        if(string.IsNullOrEmpty(avatarPath) == false)
            payload["avatarLoc"] = $"avatar{Path.GetExtension(avatarPath)}";
        StatusChanged?.Invoke(PvnServiceStatus.UploadingUserInfo);
        HttpResponseMessage response = await client.PatchAsync(new Uri(BaseUri, "user/me"), payload.ToJsonContent());
        if(response.IsSuccessStatusCode == false)
            throw new Exception(await response.Content.ReadAsStringAsync());
        JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
        account.UserDisplayName = jsonToken["userDisplayName"]!.Value<string>()!;
        account.Avatar = avatarPath;
        await _settingsService.SaveSettingAsync(KeyValues.PvnAccount, account);
        return account;
    }

    public async Task LogOutAsync()
    {
        await _settingsService.SaveSettingAsync<PvnAccount?>(KeyValues.PvnAccount, null, false, true);
    }

    private Uri GetBaseUri()
    {
        return new Uri((_settingsService.ReadSettingAsync<PvnServerType>(KeyValues.PvnServerType).Result ==
                            PvnServerType.OfficialServer
            ? _config["Urls:PotatoVNOfficialServer"]!
            : _settingsService.ReadSettingAsync<string>(KeyValues.PvnServerEndpoint).Result)!);
    }
    
    private async Task<PvnAccount?> ResolveAccount(HttpResponseMessage response, PvnAccount.LoginMethodEnum loginType)
    {
        JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
        PvnAccount account = new()
        {
            Token = jsonToken["token"]!.Value<string>()!,
            Id = jsonToken["user"]!["id"]!.Value<int>(),
            UserName = jsonToken["user"]!["userName"]!.Value<string>()!,
            UserDisplayName = jsonToken["user"]!["userDisplayName"]!.Value<string>()!,
            ExpireTimestamp = jsonToken["expire"]!.Value<long>(),
            LoginMethod = loginType
        };
        if (jsonToken["user"]!["avatar"] is not null)
        {
            StatusChanged?.Invoke(PvnServiceStatus.DownloadingAvatar);
            account.Avatar = await DownloadHelper.DownloadAndSaveImageAsync(jsonToken["user"]!["avatar"]!.Value<string>(),
                0, "PvnAvatar");
        }

        await _settingsService.SaveSettingAsync(KeyValues.PvnAccount, account);
        return account;
    }

    private async Task UploadFileAsync(string presignedUrl, string filePath)
    {
        await using FileStream fileStream = File.OpenRead(filePath);
        StreamContent content = new(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            
        HttpResponseMessage response = await _httpClient.PutAsync(presignedUrl, content);
        if(response.IsSuccessStatusCode == false)
            throw new Exception(await response.Content.ReadAsStringAsync());
    }
}

public class PvnServerInfo
{
    public required bool BangumiOauth2Enable;
    public required bool DefaultLoginEnable;
    public required bool BangumiLoginEnable;
}