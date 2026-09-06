using AutoMapper;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using PotatoVN.Client.Api;
using PotatoVN.Client.Client;
using PotatoVN.Client.Model;

namespace GalgameManager.Models.BgTasks;

public class PvnSyncTask : BgTaskBase
{
    private const int MaxConcurrentUpload = 10;
    private readonly ILocalSettingsService _settingsService = App.GetService<ILocalSettingsService>();
    private readonly IPvnService _pvnService = App.GetService<IPvnService>();
    private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
    private readonly IStaffService _staffService = App.GetService<IStaffService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private readonly IBgTaskService _bgTaskService = App.GetService<IBgTaskService>();
    private readonly IMapper _mapper = App.GetService<IMapper>();
    private PvnServerInfo _serverInfo = null!;

    public string Result { get; set; } = string.Empty;
    private readonly object _resultLock = new();

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        GalgameCollectionService gameService =
            (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!;
        IInfoService infoService = App.GetService<IInfoService>();
        IPvnService pvnService = App.GetService<IPvnService>();
        ILocalSettingsService settingsService = App.GetService<ILocalSettingsService>();

        if (settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount).Result is null)
            throw new PvnException("PvnSyncTask_Error_NotLogin".GetLocalized());

        try
        {
            ChangeProgress(0, 1, "PvnSyncTask_GettingServerInfo".GetLocalized());
            _serverInfo = (await pvnService.GetServerInfoAsync())!;
            ChangeProgress(0, 1, "PvnSyncTask_GettingModifiedTimestamp".GetLocalized());
            var lastSync = await settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncTimestamp);
            var latest = await pvnService.GetLastGalChangedTimeStampAsync();
            if (lastSync < latest)
                await PullUpdates(pvnService, gameService, settingsService, latest);
            await CommitChanges(gameService, pvnService, infoService, settingsService);
            // Staff
            if (await settingsService.ReadSettingAsync<bool>(KeyValues.SyncStaff) && _serverInfo.StaffEnable)
            {
                await PullStaffUpdates();
                await CommitStaffChanges();
            }
        }
        catch (Exception e)
        {
            var failedReason = "PvnSyncTask_Error".GetLocalized();
            if (e is HttpRequestException)
                failedReason = "PvnSyncTask_Error_Network".GetLocalized();
            ChangeProgress(-1, 1, failedReason);
            throw new PvnException($"{failedReason}\n{e}");
        }

        var notify = true;
        if (Result.IsNullOrEmpty())
        {
            Result = "PvnSyncTask_NoChange".GetLocalized();
            if (!await App.GetService<ILocalSettingsService>()
                    .ReadSettingAsync<bool>(KeyValues.EventPvnSyncEmptyNotify))
                notify = false;
        }

        if (Result.EndsWith('\n')) Result = Result[..^1];
        ChangeProgress(1, 1, Result, notify);
    }

    public override string Title { get; } = "PvnSyncTask_Title".GetLocalized();

    public override bool OnSearch(string key) => true;

    private async Task PullUpdates(IPvnService pvnService, GalgameCollectionService gameService,
        ILocalSettingsService settingsService, long latest)
    {
        IMapper mapper = App.GetService<IMapper>();
        ChangeProgress(0, 1, "PvnSyncTask_GettingModifiedList".GetLocalized());
        List<GalgameDto> changedGalgames = await pvnService.GetChangedGalgamesAsync();
        List<int> deletedGalgames = await pvnService.GetDeletedGalgamesAsync();
        if (changedGalgames.Count > 0)
        {
            ChangeProgress(0, 1, "PvnSyncTask_PullingGames".GetLocalized());
            PvnSyncTaskPullGame pullTask = new(gameService, _settingsService, _infoService, mapper);
            pullTask.Init(changedGalgames);
            await _bgTaskService.AddBgTask(pullTask);
        }

        foreach (long id in deletedGalgames)
        {
            Galgame? game = gameService.GetGalgameFromId(id.ToString(), RssType.PotatoVn);
            if (game is null) continue;
            Result += "PvnSyncTask_Pull_Deleted".GetLocalized(game.Name.Value ?? string.Empty, id) + "\n";
            await gameService.RemoveGalgame(game);
        }

        await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, latest);
    }

    private async Task CommitChanges(GalgameCollectionService gameService, IPvnService pvnService,
        IInfoService infoService,
        ILocalSettingsService settingsService)
    {
        List<int> toDelete = await settingsService.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames) ?? new();
        List<int> toDeleteCopy = new(toDelete);
        for (var index = 0; index < toDeleteCopy.Count; index++)
        {
            var id = toDeleteCopy[index];
            try
            {
                ChangeProgress(index, toDeleteCopy.Count, "PvnSyncTask_Deleting".GetLocalized(id));
                await pvnService.DeleteInternal(id);
                toDelete.Remove(id);
                Result += "PvnSyncTask_Commit_Delete".GetLocalized(id) + "\n";
            }
            catch (Exception e)
            {
                if (e is InvalidOperationException) toDelete.Remove(id); //游戏不属于该用户或游戏不存在
                // 否则为网络异常等因素，不应该移出删除列表，等待下次同步重试
                else
                    infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning,
                        "PvnSyncTask_Error_Delete".GetLocalized(id), e);
            }
            finally
            {
                await settingsService.SaveSettingAsync(KeyValues.ToDeleteGames, toDelete);
            }
        }

        List<Galgame> toUpdate;
        HashSet<Galgame> ignore = new();
        do
        {
            toUpdate = gameService.Galgames.Where(g => g.PvnUpdate).ToList();
            toUpdate.RemoveAll(ignore.Contains);
            for (var index = 0; index < toUpdate.Count; index++)
            {
                Galgame game = toUpdate[index];
                try
                {
                    PvnUploadProperties uploadProperties = game.PvnUploadProperties;
                    ChangeProgress(index, toUpdate.Count,
                        "PvnSyncTask_Uploading".GetLocalized(game.Name.Value!));
                    var id = await UploadGame(game);
                    game.Ids[(int)RssType.PotatoVn] = id.ToString();
                    await gameService.SaveGalgameAsync(game);
                    await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, DateTime.Now.ToUnixTime());
                    Result += "PvnSyncTask_Commit".GetLocalized(game.Name.Value!, id, uploadProperties.ToString()) + "\n";
                }
                catch (Exception e)
                {
                    infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning, "PvnSyncTask_Error_Upload".GetLocalized(), e);
                    ignore.Add(game);
                }
            }
        } while (toUpdate.Count > 0);

        try
        {
            //把服务器上的最新时间戳保存到本地（不使用本地时间戳是为了防止本地时间不准）
            var serverLatestChangedTimestamp = await pvnService.GetLastGalChangedTimeStampAsync();
            await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, serverLatestChangedTimestamp);
        }
        catch (Exception e)
        {
            infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning,
                "PvnSyncTask_Error_Upload_SaveSyncTime", e);
        }
    }

    private async Task PullStaffUpdates()
    {
        StaffApi api = new(await _pvnService.GetConfigAsync());
        var lastStaffChangeTime = await _settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncStaffTimestamp);
        List<StaffDto> changedList = [];
        ChangeProgress(0, 1, "PvnSyncTask_Pull_Staff_Get".GetLocalized());
        for (var currentPage = 0;; currentPage++)
        {
            StaffDtoPagedResult tmp = await api.StaffGetAsync(lastStaffChangeTime, currentPage, 25, true);
            ChangeProgress(currentPage, tmp.PageCnt, "PvnSyncTask_Pull_Staff_Get".GetLocalized());
            changedList.AddRange(tmp.Items);
            if (currentPage >= tmp.PageCnt - 1) break;
        }
        if (changedList.Count > 0)
        {
            ChangeProgress(0, 1, "PvnSyncTask_PullingStaffs".GetLocalized());
            PvnSyncTaskPullStaff pullTask = new(_staffService, _gameService, _infoService);
            pullTask.AddRange(changedList);
            await _bgTaskService.AddBgTask(pullTask);
        }

        await _settingsService.SaveSettingAsync(KeyValues.PvnSyncStaffTimestamp, DateTime.Now.ToUnixTime());
    }

    private async Task CommitStaffChanges()
    {
        do
        {
            // 将所有没有Id的staff提交到服务器
            foreach (Staff s in _staffService.GetStaffs()
                         .Where(s => string.IsNullOrEmpty(s.Ids[(int)RssType.PotatoVn])))
                s.RequirePvnSync = true;
            // 提交更改
            StaffApi api = new(await _pvnService.GetConfigAsync());
            List<Staff> toCommit = _staffService.GetStaffs().Where(s => s.RequirePvnSync).ToList();
            SemaphoreSlim semaphore = new(MaxConcurrentUpload);
            List<Task> uploadTasks = [];
            for(var i = 0; i < toCommit.Count; i++)
            {
                await semaphore.WaitAsync();
                var i1 = i;
                uploadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        Staff s = toCommit[i1];
                        ChangeProgress(i1, toCommit.Count,
                            "PvnSyncTask_Staff_Commit".GetLocalized(s.Name ?? string.Empty));
                        await UploadStaff(s, api);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
            await Task.WhenAll(uploadTasks);
            // 提交被删除的staff
            List<int> deletedStaff = await _settingsService.ReadSettingAsync<List<int>>(KeyValues.ToDeleteStaff) ?? [];
            foreach (var id in deletedStaff)
            {
                try
                {
                    await api.StaffPatchAsync(new StaffUpdateDto
                    {
                        Id = id,
                        IsDeleted = true,
                    });
                }
                catch (Exception)
                {
                    //ignore
                }
            }
            await _settingsService.SaveSettingAsync(KeyValues.ToDeleteStaff, new List<int>());
            await _settingsService.SaveSettingAsync(KeyValues.PvnSyncStaffTimestamp, DateTime.Now.ToUnixTime());
        } while (_staffService.GetStaffs().Any(s => s.RequirePvnSync));
    }

    private async Task UploadStaff(Staff s, StaffApi api)
    {
        StaffUpdateDto payload = new();
        if (!string.IsNullOrEmpty(s.Ids[(int)RssType.PotatoVn]))
            payload.Id = s.Ids[(int)RssType.PotatoVn]!.ToInt();
        if (!string.IsNullOrEmpty(s.Ids[(int)RssType.Bangumi])) payload.BgmId = s.Ids[(int)RssType.Bangumi]!;
        if (!string.IsNullOrEmpty(s.Ids[(int)RssType.Vndb])) payload.VndbId = s.Ids[(int)RssType.Vndb]!;
        if (!string.IsNullOrEmpty(s.Ids[(int)RssType.Ymgal])) payload.YmgalId = s.Ids[(int)RssType.Ymgal]!;
        if (!string.IsNullOrEmpty(s.JapaneseName)) payload.JapaneseName = s.JapaneseName;
        if (!string.IsNullOrEmpty(s.EnglishName)) payload.EnglishName = s.EnglishName;
        if (!string.IsNullOrEmpty(s.ChineseName)) payload.ChineseName = s.ChineseName;
        payload.Gender = (PotatoVN.Client.Model.Gender)s.Gender;
        if (!string.IsNullOrEmpty(s.Description)) payload.Description = s.Description;
        if (s.BirthDate != DateTime.MinValue && s.BirthDate is not null)
            payload.BirthDateTimestamp = s.BirthDate.Value.ToUnixTime();
        payload.StaffGames = s.Games.Where(sg => !string.IsNullOrEmpty(sg.Game.Ids[(int)RssType.PotatoVn]))
            .Select(sg => new StaffGameUpdateDto
            {
                GameId = sg.Game.Ids[(int)RssType.PotatoVn]!.ToInt(),
                Relation = sg.Relation.Select(r => (PotatoVN.Client.Model.Career)r).ToList()
            }).ToList();
        // 优先使用外部图片链接
        if (!string.IsNullOrEmpty(s.ImageUrl)) payload.ExternalImageLink = s.ImageUrl;
        else if (!string.IsNullOrEmpty(s.ImagePath) && !string.IsNullOrEmpty(s.Name))
            payload.ImageLoc = (await _pvnService.UploadFileAsync(s.ImagePath, $"staff/{s.Name}"))!;

        try
        {
            StaffDto tmp = await api.StaffPatchAsync(payload);
            s.Ids[(int)RssType.PotatoVn] = tmp.Id.ToString();
            lock (_resultLock)
            {
                Result += "PvnSyncTask_Staff_Commit_Success".GetLocalized(s.Name ?? string.Empty, tmp.Id) + "\n";
            }
        }
        catch (Exception)
        {
            //ignore
        }
        await _settingsService.SaveSettingAsync(KeyValues.PvnSyncStaffTimestamp, DateTime.Now.ToUnixTime());
        s.RequirePvnSync = false;
        _staffService.Save(s, false);
    }

    private async Task<int> UploadGame(Galgame galgame, bool isRetry = false)
    {
        if (galgame.PvnUpdate == false) throw new Exception("Galgame not marked as updated."); //不应该发生
        PvnAccount? account = await _settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is null) throw new InvalidOperationException("PotatoVN account is not login."); //不应该发生
        GalgameUpdateDto payload = new();

        if (galgame.Ids[(int)RssType.PotatoVn].IsNullOrEmpty() == false &&
            int.TryParse(galgame.Ids[(int)RssType.PotatoVn], out var id))
            payload.Id = id;

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.Infos))
        {
            payload.BgmId = galgame.Ids[(int)RssType.Bangumi]!;
            payload.VndbId = galgame.Ids[(int)RssType.Vndb]!;
            payload.Name = galgame.Name.Value!;
            payload.CnName = galgame.CnName;
            payload.Description = galgame.Description.Value!;
            payload.Developer = galgame.Developer.Value!;
            payload.ExpectedPlayTime = galgame.ExpectedPlayTime.Value!;
            payload.Rating = galgame.Rating.Value;
            payload.ReleaseDateTimeStamp = galgame.ReleaseDate.Value.Date.ToUnixTime();
            payload.Tags = (galgame.Tags.Value ?? []).ToList();
        }

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.ImageLoc) &&
            string.IsNullOrEmpty(galgame.ImagePath) == false && galgame.ImagePath != Galgame.DefaultImagePath)
        {
            var ossPath = await _pvnService.UploadFileAsync(galgame.ImagePath.Value, $"{galgame.Name.Value}/cover", e =>
            {
                _infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning,
                    "PvnService_UploadImageFailed".GetLocalized(galgame.Name.Value ?? string.Empty), e);
            });
            if (ossPath is not null) payload.ImageLoc = ossPath;
        }

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.HeaderImageLoc) &&
            !string.IsNullOrEmpty(galgame.HeaderImagePath.Value) &&
            await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncHeaderImage))
        {
            if (string.IsNullOrEmpty(galgame.HeaderImageUrl)) //由用户手动指定的header Image
            {
                var ossPath = await _pvnService.UploadFileAsync(galgame.HeaderImagePath.Value, $"{galgame.Name.Value}/header", e =>
                {
                    _infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning,
                        "PvnService_UploadImageFailed".GetLocalized(galgame.Name.Value ?? string.Empty), e);
                });
                if (ossPath is not null) payload.HeaderImageOssLoc = ossPath;
            }
            else
            {
                // Use external URL
                payload.HeaderImageExternalUrl = galgame.HeaderImageUrl;
            }
        }

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.Review))
        {
            payload.PlayType = (PotatoVN.Client.Model.PlayType)(int)galgame.PlayType;
            payload.Comment = galgame.Comment;
            payload.MyRate = galgame.MyRate;
            payload.PrivateComment = galgame.PrivateComment;
        }

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.PlayTime))
        {
            payload.TotalPlayTime = galgame.TotalPlayTime;
            payload.PlayCount = galgame.PlayCount; // Add PlayCount here
            List<PlayLogDto> logs = new();
            foreach (KeyValuePair<string, int> pair in galgame.PlayedTime)
                if(DateTimeExtensions.ToDateTime(pair.Key) != DateTime.MinValue)
                    logs.Add(new PlayLogDto
                    {
                        DateTimeStamp = DateTimeExtensions.ToDateTime(pair.Key).Date.ToUnixTime(),
                        Minute = pair.Value
                    });
            payload.PlayTime = logs;
        }

        if (galgame.PvnUploadProperties.HasFlag(PvnUploadProperties.Character) && _serverInfo.GalgameStaffAvailable
            && await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncGameCharacters))
        {
            payload.Characters = [];
            List<Task> uploadImgTasks = [];
            foreach (GalgameCharacter c in galgame.Characters)
            {
                CharacterUpdateDto dto = _mapper.Map<CharacterUpdateDto>(c);
                payload.Characters.Add(dto);
                uploadImgTasks.Add(Task.Run(() =>
                {
                    dto.ImageLoc = _pvnService.UploadFileAsync(c.ImagePath,
                        DownloadHelper.GetCharacterImageFileName(galgame.Uuid, c.Name)).Result!;
                    dto.PreviewImageLoc = _pvnService.UploadFileAsync(c.PreviewImagePath,
                        DownloadHelper.GetCharacterImageFileName(galgame.Uuid, c.Name, preview: true)).Result!;
                }));
            }
            await Task.WhenAll(uploadImgTasks.ToArray());
        }

        GalgameApi api = new(await _pvnService.GetConfigAsync());
        try
        {
            GalgameDto result = await api.GalgamePatchAsync(payload);
            galgame.PvnUpdate = false;
            galgame.PvnUploadProperties = PvnUploadProperties.None;
            return result.Id;
        }
        catch (ApiException e)
        {
            if (isRetry) throw new HttpRequestException(e.ErrorContent.ToString());
            switch (e.ErrorCode)
            {
                //以下情况应该清除id后再重试
                case 404: //Not Found：该id游戏不存在（导入了旧数据 or 来自其他服务器）
                case 400: //Bad Request：不是该id游戏的拥有者（导入的数据是来自别人的or别的服务器的）
                    galgame.Ids[(int)RssType.PotatoVn] = null;
                    _infoService.DeveloperEvent(
                        msg: $"Game {galgame.Name.Value} is not found on server, remove id and retry.");
                    return await UploadGame(galgame, true);
                default:
                    throw new HttpRequestException(e.ErrorContent.ToString());
            }
        }
    }
}
