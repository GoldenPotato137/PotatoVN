using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml.Controls;
using PotatoVN.Client.Api;
using PotatoVN.Client.Client;
using PotatoVN.Client.Model;
using IMapper = AutoMapper.IMapper;
using PlayType = PotatoVN.Client.Model.PlayType;

namespace GalgameManager.Services;

public class PvnService : IPvnService
{
    private readonly IMapper _mapper;
    private readonly ILocalSettingsService _settingsService;
    private readonly IBgmOAuthService _bgmService;
    private readonly IConfiguration _config;
    private readonly IBgTaskService _bgTaskService;
    private readonly HttpClient _httpClient;
    private readonly IGalgameCollectionService _gameService;
    private readonly IStaffService _staffService;
    private readonly IInfoService _infoService;
    public Uri BaseUri { get; private set; }
    public Action<PvnServiceStatus>? StatusChanged { get; set; }
    public PvnSyncTask? SyncTask { get; private set; }

    private PvnServerInfo? _serverInfo;

    public PvnService(ILocalSettingsService settingsService, IConfiguration config, IBgmOAuthService bgmService,
        IBgTaskService bgTaskService, IGalgameCollectionService gameService, IStaffService staffService,
        IInfoService infoService, IMapper mapper)
    {
        _mapper = mapper;
        _settingsService = settingsService;
        _bgmService = bgmService;
        _config = config;
        BaseUri = GetBaseUri();
        _bgTaskService = bgTaskService;
        _gameService = gameService;
        _staffService = staffService;
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
        _gameService.GalgameMutated += (_, args) =>
        {
            if (!args.Changes.HasFlag(GalgameChangeKind.Added) || args.Origin == GalgameChangeOrigin.PvnSync)
                return;
            SyncGames();
        };
        _gameService.GalgameDeletedEvent += async galgame =>
        {
            List<int> list = await _settingsService.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames) ?? new();
            if(galgame.Ids[(int)RssType.PotatoVn] is null) return;
            list.Add(Convert.ToInt32(galgame.Ids[(int)RssType.PotatoVn]));
            await _settingsService.SaveSettingAsync(KeyValues.ToDeleteGames, list);
            SyncGames();
        };
        _staffService.OnStaffSaved += staff =>
        {
            if (!_settingsService.ReadSettingAsync<bool>(KeyValues.SyncStaff).Result) return;
            staff.RequirePvnSync = true;
            _staffService.Save(staff, false);
            SyncGames();
        };
        _staffService.OnStaffDeleted += staff =>
        {
            if (!_settingsService.ReadSettingAsync<bool>(KeyValues.SyncStaff).Result) return;
            List<int> list = _settingsService.ReadSettingAsync<List<int>>(KeyValues.ToDeleteStaff).Result ?? [];
            if(staff.Ids[(int)RssType.PotatoVn]?.ToInt() is not { } id) return;
            list.Add(id);
            _settingsService.SaveSettingAsync(KeyValues.ToDeleteStaff, list);
            SyncGames();
        };
    }
    
    public void Startup()
    {
        Task.Run(async () =>
        {
            PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
            if (account is not null && DateTime.Now.ToUnixTime() >= account.RefreshTimestamp)
            {
                try
                {
                    await RefreshTokenAsync();
                    _infoService.Event(EventType.PvnAccountEvent, InfoBarSeverity.Success,
                        "PvnService_TokenRefreshed".GetLocalized());
                }
                catch (Exception e)
                {
                    _infoService.Event(EventType.PvnAccountEvent, InfoBarSeverity.Warning,
                        "PvnService_TokenRefreshFailed".GetLocalized(), e);
                }
            }
            SyncGames();
            if (await _settingsService.ReadSettingAsync<bool>(KeyValues.FingerprintPlanEnabled))
            {
                if (_bgTaskService.GetBgTask<FingerprintUploadTask>(string.Empty) is null)
                    await _bgTaskService.AddBgTask(new FingerprintUploadTask());
            }
        });
    }

    public async Task<PvnServerInfo?> GetServerInfoAsync()
    {
        if (_serverInfo is null)
        {
            ServerApi api = new(_httpClient, await GetConfigAsync());
            ServerInfoDto result = await api.ServerInfoGetAsync();
            _serverInfo = new PvnServerInfo
            {
                BangumiOauth2Enable = result.BangumiOAuth2Enable,
                DefaultLoginEnable = result.DefaultLoginEnable,
                BangumiLoginEnable = result.BangumiLoginEnable,
                GalgameStaffAvailable = result.GalgameStaffAvailable,
                StaffEnable = result.StaffEnable,
            };
        }
        
        return _serverInfo;
    }

    public async Task<PvnAccount?> LoginAsync(string username, string password)
    {
        UserApi api = new(_httpClient, await GetConfigAsync());
        try
        {
            UserWithTokenDto dto = await api.UserSessionPostAsync(new UserLoginDto(username, password));
            return await ResolveAccount(dto, PvnAccount.LoginMethodEnum.Default);
        }
        catch (ApiException e)
        {
            if (e.ErrorCode == 400) throw new InvalidOperationException("PvnService_PasswordIncorrect".GetLocalized());
            if (e.ErrorCode == 503) throw new InvalidOperationException("PvnService_ServerUnavailable".GetLocalized());
            throw;
        }
    }
    
    public async Task<PvnAccount?> RegisterAsync(string username, string password)
    {
        UserApi api = new(_httpClient, await GetConfigAsync());
        try
        {
            UserWithTokenDto result =  await api.UserPostAsync(new UserRegisterDto(username, password));
            return await ResolveAccount(result, PvnAccount.LoginMethodEnum.Default);
        }
        catch (ApiException e)
        {
            switch (e.ErrorCode)
            {
                case 400:
                    throw new InvalidOperationException("PvnService_UserNameAlreadyTaken".GetLocalized());
                case 503:
                    throw new InvalidOperationException("PvnService_ServerUnavailable".GetLocalized());
                default:
                    throw;
            }
        }
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

        UserApi api = new(_httpClient, await GetConfigAsync());
        try
        {
            UserWithTokenDto result = await api.UserSessionBgmPostAsync(new UserLoginViaBgmDto(bgmAccount.BangumiAccessToken));
            return await ResolveAccount(result, PvnAccount.LoginMethodEnum.Bangumi);
        }
        catch (ApiException e)
        {
            switch (e.ErrorCode)
            {
                case 400:
                    throw new InvalidOperationException("Bangumi token invalid."); //不应该发生
                case 502:
                    throw new InvalidOperationException("PvnService_ServerCannotConnectBgm".GetLocalized());
                case 503:
                    throw new InvalidOperationException("PvnService_ServerUnavailable".GetLocalized());
                default:
                    throw;
            }
        }
    }

    public async Task<PvnAccount?> RefreshTokenAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null) throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        UserApi api = new(_httpClient, await GetConfigAsync());
        try
        {
            UserWithTokenDto result = await api.UserSessionRefreshGetAsync();
            return await ResolveAccount(result, account.LoginMethod);
        }
        catch (ApiException e)
        {
            if (e.ErrorCode == (int)HttpStatusCode.Unauthorized)
                throw new AuthenticationException("PvnService_AccountTokenExpire".GetLocalized());
            throw;
        }
    }

    public async Task<PvnAccount?> ModifyAccountAsync(string? userDisplayName, string? avatarPath, string? newPassword,
        string? oldPassword)
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        var ossFilePath = string.Empty;
        if (avatarPath is not null)
        {
            _infoService.Info(InfoBarSeverity.Informational,"PvnService_UploadingAvatar".GetLocalized());
            ossFilePath = await UploadFileAsync(avatarPath, "Avatar", e =>
            {
                _infoService.Event(EventType.PvnAccountEvent, InfoBarSeverity.Warning,
                    "PvnService_UploadAvatarFailed".GetLocalized(), e);
                avatarPath = null;
            }) ?? string.Empty;
        }

        try
        {
            UserApi api = new(_httpClient, await GetConfigAsync());
            UserModifyDto payload = new();
            if(string.IsNullOrEmpty(userDisplayName) == false)
                payload.UserDisplayName = userDisplayName;
            if(string.IsNullOrEmpty(newPassword) == false)
            {
                payload.NewPassword = newPassword;
                payload.OldPassword = oldPassword ?? throw new ArgumentNullException(nameof(oldPassword));
            }
            if(string.IsNullOrEmpty(avatarPath) == false)
                payload.AvatarLoc = ossFilePath;
            StatusChanged?.Invoke(PvnServiceStatus.UploadingUserInfo);
            UserDto result = await api.UserMePatchAsync(payload);
            account.UserDisplayName = result.UserDisplayName;
            account.Avatar = avatarPath;
            await _settingsService.SaveSettingAsync(KeyValues.PvnAccount, account);
            return account;
        }
        catch (ApiException e)
        {
            throw new Exception(e.ErrorContent.ToString());
        }
    }
    
    public async Task<long> GetLastGalChangedTimeStampAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        try
        {
            UserApi api = new (_httpClient, await GetConfigAsync());
            UserDto result = await api.UserMeGetAsync(false);
            //顺便更新用户信息
            account.UserName = result.UserName;
            account.UserDisplayName = result.UserDisplayName;
            account.TotalSpace = result.TotalSpace;
            account.UsedSpace = result.UsedSpace;
            await _settingsService.SaveSettingAsync(KeyValues.PvnAccount, account);

            return result.LastGalChangedTimeStamp;
        }
        catch (ApiException e)
        {
            if (e.ErrorCode == (int)HttpStatusCode.Unauthorized)
                throw new AuthenticationException("PvnService_AccountTokenExpire".GetLocalized());
            throw;
        }
    }

    public async Task<List<GalgameDto>> GetChangedGalgamesAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        GalgameApi api = new (_httpClient, await GetConfigAsync());
        List<GalgameDto> result = new();
        int pageCnt, pageIndex = 0;
        var timestamp = await _settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncTimestamp);
        do
        {
            GalgameDtoPagedResult tmp = await api.GalgameGetAsync(timestamp, pageIndex, 25);
            pageCnt = tmp.PageCnt;
            result.AddRange(tmp.Items);
        } while (++pageIndex < pageCnt);
        return result;
    }

    public async Task<List<int>> GetDeletedGalgamesAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null)
            throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        GalgameApi api = new (_httpClient, await GetConfigAsync());
        List<int> result = new();
        int pageCnt, pageIndex = 0;
        var timestamp = await _settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncTimestamp);
        do
        {
            GalgameDeletedDtoPagedResult tmp = await api.GalgameDeletedGetAsync(timestamp, pageIndex, 25);
            pageCnt = tmp.PageCnt;
            result.AddRange(tmp.Items.Select(dto => dto.GalgameId));
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
    
    public async Task DeleteInternal(int pvnId)
    {
        PvnAccount? account = _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount).Result;
        if (account is null) throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        try
        {
            GalgameApi api = new(_httpClient, await GetConfigAsync());
            await api.GalgameGalgameIdDeleteAsync(pvnId);
        }
        catch (ApiException e)
        {
            switch (e.ErrorCode)
            {
                case 400:
                case 404:
                    throw new InvalidOperationException();
            }
            throw;
        }
    }

    public async Task LogOutAsync()
    {
        await _settingsService.SaveSettingAsync<PvnAccount?>(KeyValues.PvnAccount, null, false, true);
        await _settingsService.SaveSettingAsync(KeyValues.SyncGames, false);
        await _settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, 0);
        await _settingsService.SaveSettingAsync(KeyValues.PvnSyncStaffTimestamp, 0);
        await _settingsService.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int>());
        foreach (Galgame gal in _gameService.Galgames)
        {
            gal.Ids[(int)RssType.PotatoVn] = null;
            gal.PvnLastCharacterFetchTime = 0;
            await _gameService.SaveGalgameAsync(gal);
        }
        foreach (Staff staff in _staffService.GetStaffs())
        {
            staff.Ids[(int)RssType.PotatoVn] = null;
            _staffService.Save(staff, false);
        }
    }

    private Uri GetBaseUri()
    {
        return new Uri((_settingsService.ReadSettingAsync<PvnServerType>(KeyValues.PvnServerType).Result ==
                            PvnServerType.OfficialServer
            ? _config["Urls:PotatoVNOfficialServer"]!
            : _settingsService.ReadSettingAsync<string>(KeyValues.PvnServerEndpoint).Result)!);
    }

    private async Task<PvnAccount?> ResolveAccount(UserWithTokenDto dto, PvnAccount.LoginMethodEnum loginType)
    {
        PvnAccount account = new()
        {
            Token = dto.Token,
            Id = dto.User.Id,
            UserName = dto.User.UserName,
            UserDisplayName = dto.User.UserDisplayName,
            ExpireTimestamp = dto.Expire,
            LoginMethod = loginType,
            TotalSpace = dto.User.TotalSpace,
            UsedSpace = dto.User.UsedSpace,
        };
        if (dto.User.Avatar is { } str)
        {
            StatusChanged?.Invoke(PvnServiceStatus.DownloadingAvatar);
            Exception? failedException = null;
            account.Avatar = await DownloadHelper.DownloadAndSaveImageWithDiffThread(str,
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
    
    public async Task<string?> UploadFileAsync(string? filePath, string? targetName = null,
        Action<Exception>? onFailed = null)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        var size = new FileInfo(filePath).Length;
        if (account is null || account.TotalSpace - account.UsedSpace <= size) return null;
        string presignedUrl = string.Empty, ossFilePath = string.Empty;
        try
        {
            await GetPresignedUrl(targetName ?? Path.GetFileName(filePath));
            await UploadFile();
            return ossFilePath;
        }
        catch (Exception e)
        {
            onFailed?.Invoke(e);
            return null;
        }
        finally
        {
            if (!string.IsNullOrEmpty(ossFilePath))
                await AfterUploadFileAsync();
        }
        
        async Task GetPresignedUrl(string saveName)
        {
            OssApi api = new(_httpClient, await GetConfigAsync());
            try
            {
                presignedUrl = await api.OssPutGetAsync($"{saveName}{Path.GetExtension(filePath)}", size);
                presignedUrl = presignedUrl.Trim('\"').TrimEnd('\"');
                ossFilePath = $"{saveName}{Path.GetExtension(filePath)}";
            }
            catch (ApiException e)
            {
                throw new Exception($"Get Oss presigned url failed with code {e.ErrorCode}.");
            }
        }

        async Task UploadFile()
        {
            await using FileStream fileStream = File.OpenRead(filePath);
            StreamContent content = new(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            HttpResponseMessage response = await _httpClient.PutAsync(presignedUrl, content);
            if (response.IsSuccessStatusCode == false)
                throw new Exception(await response.Content.ReadAsStringAsync());
        }

        async Task AfterUploadFileAsync()
        {
            try
            {
                OssApi api = new(_httpClient, await GetConfigAsync());
                UserDto result = await api.OssUpdatePutAsync(ossFilePath);
                account.UsedSpace = result.UsedSpace;
                account.TotalSpace = result.TotalSpace;
                await _settingsService.SaveSettingAsync(KeyValues.PvnAccount, account);
            }
            catch (Exception e)
            {
                _infoService.DeveloperEvent(msg: "Failed to update user space.", e: e);
            }
        }
    }

    private void StartSyncTask()
    {
        if (_settingsService.ReadSettingAsync<PvnAccount?>(KeyValues.PvnAccount).Result is null) return;
        if (!_settingsService.ReadSettingAsync<bool>(KeyValues.SyncGames).Result) return;
        
        SyncTask = _bgTaskService.GetBgTask<PvnSyncTask>(string.Empty);
        if (SyncTask is not null) return;
        SyncTask = new PvnSyncTask();
        _bgTaskService.AddBgTask(SyncTask);
    }

    public async Task<Configuration> GetConfigAsync()
    {
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        Configuration result = new()
        {
            BasePath = BaseUri.ToString().TrimEnd('/'),
        };
        if (account is not null)
            result.ApiKey["Authorization"] = $"Bearer {account.Token}";
        return result;
    }
}

public class PvnServerInfo
{
    public required bool BangumiOauth2Enable;
    public required bool DefaultLoginEnable;
    public required bool BangumiLoginEnable;
    public required bool GalgameStaffAvailable;
    public required bool StaffEnable;
}
