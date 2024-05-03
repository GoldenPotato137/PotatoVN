using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;

public class PvnService : IPvnService
{
    private readonly ILocalSettingsService _settingsService;
    private readonly IBgmOAuthService _bgmService;
    private readonly IConfiguration _config;
    private readonly IBgTaskService _bgTaskService;
    private readonly HttpClient _httpClient;
    private readonly GalgameCollectionService _gameService;
    private readonly IInfoService _infoService;
    public Uri BaseUri { get; private set; }
    public Action<PvnServiceStatus>? StatusChanged { get; set; }
    public PvnSyncTask? SyncTask { get; private set; }

    private PvnServerInfo? _serverInfo;

    public PvnService(ILocalSettingsService settingsService, IConfiguration config, IBgmOAuthService bgmService,
        IBgTaskService bgTaskService, IDataCollectionService<Galgame> gameService, IInfoService infoService)
    {
        _settingsService = settingsService;
        _bgmService = bgmService;
        _config = config;
        BaseUri = GetBaseUri();
        _bgTaskService = bgTaskService;
        _gameService = (GalgameCollectionService)gameService;
        _httpClient = Utils.GetDefaultHttpClient();
        _infoService = infoService;

        _settingsService.OnSettingChanged += async (key, _) =>
        {
            if (key is KeyValues.PvnServerType or KeyValues.PvnServerEndpoint)
            {
                BaseUri = GetBaseUri();
                _serverInfo = null;
                await LogOutAsync();
            }
        };
        _gameService.GalgameAddedEvent += _ => SyncGames();
        _gameService.GalgameDeletedEvent += async galgame =>
        {
            List<int> list = await _settingsService.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames) ?? new();
            if(galgame.Ids[(int)RssType.PotatoVn] is null) return;
            list.Add(Convert.ToInt32(galgame.Ids[(int)RssType.PotatoVn]));
            await _settingsService.SaveSettingAsync(KeyValues.ToDeleteGames, list);
            SyncGames();
        };
    }
    
    public void Startup()
    {
        SyncGames();
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
        BgmAccount? bgmAccount = await _settingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount);
        Task getBgmAccountTask = Task.CompletedTask;
        if (bgmAccount is null || bgmAccount.OAuthed == false)
        {
            await _bgmService.StartOAuthAsync();
            getBgmAccountTask = Task.Run(async () =>
            {
                for(var i = 0; i <= 60 * 5; i++)  //timeout: 60sec
                {
                    await Task.Delay(200);
                    bgmAccount = await _settingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount);
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
        var ossFilePath = string.Empty;
        if (avatarPath is not null)
        {
            try
            {
                StatusChanged?.Invoke(PvnServiceStatus.UploadingAvatar);
                (string presignedUrl, string ossFilePath) result = await GetPresignedUrl(avatarPath, "Avatar", client);
                ossFilePath = result.ossFilePath;
                await UploadFileAsync(result.presignedUrl, avatarPath);
            }
            catch(Exception e)
            {
                _infoService.Event(EventType.PvnAccountEvent, InfoBarSeverity.Warning,
                    "PvnService_UploadAvatarFailed".GetLocalized(), e);
                avatarPath = null;
            }
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
            payload["avatarLoc"] = ossFilePath;
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
    
    public async Task<long> GetLastGalChangedTimeStampAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        HttpClient client = Utils.GetDefaultHttpClient().AddToken(account.Token);
        HttpResponseMessage response =
            await client.GetAsync(new Uri(BaseUri, "user/me").AddQuery("withAvatar", "false"));
        if(response.StatusCode == HttpStatusCode.Unauthorized)
            throw new AuthenticationException("PotatoVN account token invalid.");
        response.EnsureSuccessStatusCode();
        JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
        //顺便更新用户信息
        account.UserName = jsonToken["userName"]!.Value<string>()!;
        account.UserDisplayName = jsonToken["userDisplayName"]!.Value<string>()!;
        account.TotalSpace = jsonToken["totalSpace"]!.Value<long>();
        account.UsedSpace = jsonToken["usedSpace"]!.Value<long>();
        await _settingsService.SaveSettingAsync(KeyValues.PvnAccount, account);
        
        return jsonToken["lastGalChangedTimeStamp"]!.Value<long>();
    }

    public async Task<List<GalgameDto>> GetChangedGalgamesAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        HttpClient client = Utils.GetDefaultHttpClient().AddToken(account.Token);
        List<GalgameDto> result = new();
        int pageCnt, pageIndex = 0;
        var timestamp = await _settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncTimestamp);
        do
        {
            HttpResponseMessage response = await client.GetAsync(new Uri(BaseUri, "galgame")
                .AddQuery("timestamp", timestamp.ToString())
                .AddQuery("pageIndex", pageIndex.ToString()).AddQuery("pageSize", "25"));
            response.EnsureSuccessStatusCode();
            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            pageCnt = jsonToken["pageCnt"]!.Value<int>();
            List<GalgameDto> tmp = jsonToken["items"]!.ToObject<List<GalgameDto>>()!;
            result.AddRange(tmp);
        } while (++pageIndex < pageCnt);
        return result;
    }

    public async Task<List<int>> GetDeletedGalgamesAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        HttpClient client = Utils.GetDefaultHttpClient().AddToken(account.Token);
        List<int> result = new();
        int pageCnt, pageIndex = 0;
        var timestamp = await _settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncTimestamp);
        do
        {
            HttpResponseMessage response = await client.GetAsync(new Uri(BaseUri, "galgame/deleted")
                .AddQuery("timestamp", timestamp.ToString())
                .AddQuery("pageIndex", pageIndex.ToString()).AddQuery("pageSize", "25"));
            response.EnsureSuccessStatusCode();
            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            pageCnt = jsonToken["pageCnt"]!.Value<int>();
            List<GalgameDeleteDto> tmp = jsonToken["items"]!.ToObject<List<GalgameDeleteDto>>()!;
            result.AddRange(tmp.Select(dto => dto.galgameId));
        } while (++pageIndex < pageCnt);
        return result;
    }

    public void SyncGames()
    {
        if (_settingsService.ReadSettingAsync<bool>(KeyValues.SyncGames).Result == false) return;
        foreach (Galgame galgame in _gameService.Galgames.Where(g => g.Ids[(int)RssType.PotatoVn].IsNullOrEmpty()))
        {
            galgame.PvnUpdate = true;
            galgame.PvnUploadProperties = PvnUploadProperties.All;
        }
        StartSyncTask();
    }

    public void Upload(Galgame galgame, PvnUploadProperties properties)
    {
        galgame.PvnUpdate = true;
        galgame.PvnUploadProperties |= properties;
        StartSyncTask();
    }

    public async Task<int> UploadInternal(Galgame galgame)
    {
        if (galgame.PvnUpdate == false) throw new Exception("Galgame not marked as updated."); //不应该发生
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null) throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        HttpClient client = Utils.GetDefaultHttpClient().AddToken(account.Token);
        Dictionary<string, object?> payload = new();
        
        if (galgame.Ids[(int)RssType.PotatoVn].IsNullOrEmpty() == false)
            payload["id"] = galgame.Ids[(int)RssType.PotatoVn]!;

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.Infos))
        {
            payload["bgmId"] = galgame.Ids[(int)RssType.Bangumi];
            payload["vndbId"] = galgame.Ids[(int)RssType.Vndb];
            payload["name"] = galgame.Name.Value;
            payload["cnName"] = galgame.CnName;
            payload["description"] = galgame.Description.Value;
            payload["developer"] = galgame.Developer.Value;
            payload["expectedPlayTime"] = galgame.ExpectedPlayTime.Value;
            payload["rating"] = galgame.Rating.Value;
            payload["releaseDateTimeStamp"] = galgame.ReleaseDate.Value.Date.ToUnixTime();
            payload["tags"] = galgame.Tags.Value;
        }
        
        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.ImageLoc) &&
            string.IsNullOrEmpty(galgame.ImagePath) == false && galgame.ImagePath != Galgame.DefaultImagePath)
        {
            try
            {
                (string url, string path) tmp = await GetPresignedUrl(galgame.ImagePath!, galgame.Name!, client);
                await UploadFileAsync(tmp.url, galgame.ImagePath!);
                payload["imageLoc"] = tmp.path;
            }
            catch(Exception e)
            {
                _infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning,
                    "PvnService_UploadImageFailed".GetLocalized(galgame.Name.Value ?? string.Empty), e);
            }
        }

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.Review))
        {
            payload["playType"] = galgame.PlayType;
            payload["comment"] = galgame.Comment;
            payload["myRate"] = galgame.MyRate;
            payload["privateComment"] = galgame.PrivateComment;
        }
        
        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.PlayTime))
        {
            payload["totalPlayTime"] = galgame.TotalPlayTime;
            List<PlayLogDto> logs = new();
            foreach (KeyValuePair<string, int> pair in galgame.PlayedTime)
                if(DateTimeExtensions.ToDateTime(pair.Key) != DateTime.MinValue)
                    logs.Add(new PlayLogDto
                    {
                        dateTimeStamp = DateTimeExtensions.ToDateTime(pair.Key).Date.ToUnixTime(),
                        minute = pair.Value
                    });
            payload["playTime"] = logs;
        }

        HttpResponseMessage result =
            await client.PatchAsync(new Uri(BaseUri, "galgame"), payload.ToJsonContent());
        if (result.IsSuccessStatusCode == false)
            throw new HttpRequestException(await result.Content.ReadAsStringAsync());

        galgame.PvnUpdate = false;
        galgame.PvnUploadProperties = PvnUploadProperties.None;
        return JToken.Parse(await result.Content.ReadAsStringAsync())["id"]!.Value<int>();
    }

    public async Task DeleteInternal(int pvnId)
    {
        PvnAccount? account = _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount).Result;
        if (account is null) throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        HttpClient client = Utils.GetDefaultHttpClient().AddToken(account.Token);
        await client.DeleteAsync(new Uri(BaseUri, $"galgame/{pvnId}"));
    }

    public async Task LogOutAsync()
    {
        await _settingsService.SaveSettingAsync<PvnAccount?>(KeyValues.PvnAccount, null, false, true);
        await _settingsService.SaveSettingAsync(KeyValues.SyncGames, false);
        await _settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, 0);
        foreach (Galgame gal in _gameService.Galgames)
            gal.Ids[(int)RssType.PotatoVn] = null;
        await _gameService.SaveGalgamesAsync();
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
            LoginMethod = loginType,
            TotalSpace = jsonToken["user"]!["totalSpace"]!.Value<long>(),
            UsedSpace = jsonToken["user"]!["usedSpace"]!.Value<long>(),
        };
        if (jsonToken["user"]!["avatar"]?.Value<string>() is not null)
        {
            StatusChanged?.Invoke(PvnServiceStatus.DownloadingAvatar);
            Exception? failedException = null;
            account.Avatar = await DownloadHelper.DownloadAndSaveImageAsync(jsonToken["user"]!["avatar"]!.Value<string>(),
                0, "PvnAvatar", onException: e => failedException = e);
            if (account.Avatar is null)
            {
                _infoService.Event(EventType.PvnAccountEvent, InfoBarSeverity.Warning,
                    "PvnService_DownloadAvatarFailed".GetLocalized(), failedException);
            }
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

    private async Task<(string presignedUrl, string ossFilePath)> GetPresignedUrl(string filePath, string saveName,
        HttpClient client)
    {
        HttpResponseMessage tmp = await client.GetAsync(
            new Uri(BaseUri, "oss/put").AddQuery("objectFullName", $"{saveName}{Path.GetExtension(filePath)}"));
        if (tmp.IsSuccessStatusCode == false)
            throw new Exception($"Get Oss presigned url failed with code {tmp.StatusCode}.");
        var presignedUrl = await tmp.Content.ReadAsStringAsync();
        return (presignedUrl, $"{saveName}{Path.GetExtension(filePath)}");
    }

    private void StartSyncTask()
    {
        SyncTask = _bgTaskService.GetBgTask<PvnSyncTask>(string.Empty);
        if (SyncTask is not null && SyncTask.IsRunning) return;
        SyncTask = new PvnSyncTask();
        _bgTaskService.AddBgTask(SyncTask);
    }
}

public class PvnServerInfo
{
    public required bool BangumiOauth2Enable;
    public required bool DefaultLoginEnable;
    public required bool BangumiLoginEnable;
}