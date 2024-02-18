using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class PvnSyncTask : BgTaskBase
{
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    public override Task Run()
    {
        GalgameCollectionService gameService =
            (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
        IInfoService infoService = App.GetService<IInfoService>();
        IPvnService pvnService = App.GetService<IPvnService>();
        ILocalSettingsService settingsService = App.GetService<ILocalSettingsService>();

        if (settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount).Result is null)
        {
            ChangeProgress(-1, 1, "PvnSyncTask_Error_NotLogin".GetLocalized());
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            try
            {
                ChangeProgress(0, 1, "PvnSyncTask_GettingModifiedTimestamp".GetLocalized());
                var lastSync = await settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncTimestamp);
                var latest = await pvnService.GetLastGalChangedTimeStampAsync();
                if (lastSync < latest)
                    await PullUpdates(pvnService, gameService, settingsService, latest);
            }
            catch (Exception e)
            {
                InfoBarSeverity severity = InfoBarSeverity.Error;
                if (e is HttpRequestException)
                    severity = InfoBarSeverity.Warning;
                infoService.Event(severity, "PvnSyncTask_Error".GetLocalized(), e);
                var failedReason = "PvnSyncTask_Error".GetLocalized();
                if (e is HttpRequestException)
                    failedReason = "PvnSyncTask_Error_Network".GetLocalized();
                ChangeProgress(-1, 1, failedReason);
                return;
            }

            await CommitChanges(gameService, pvnService, infoService, settingsService);

            ChangeProgress(1, 1, "PvnSyncTask_Completed".GetLocalized());
            await Task.Delay(5000);
        });
    }

    public override string Title { get; } = "PvnSyncTask_Title".GetLocalized();

    public override bool OnSearch(string key) => true;

    private async Task PullUpdates(IPvnService pvnService, GalgameCollectionService gameService,
        ILocalSettingsService settingsService, long latest)
    {
        ChangeProgress(0, 1, "PvnSyncTask_GettingModifiedList".GetLocalized());
        List<GalgameDto> changedGalgames = await pvnService.GetChangedGalgamesAsync();
        List<int> deletedGalgames = await pvnService.GetDeletedGalgamesAsync();
        for (var index = 0; index < changedGalgames.Count; index++)
        {
            GalgameDto dto = changedGalgames[index];
            Galgame? game = gameService.GetGalgameFromId(dto.id.ToString(), RssType.PotatoVn);
            game ??= gameService.GetGalgameFromId(dto.bgmId, RssType.Bangumi);
            game ??= gameService.GetGalgameFromId(dto.vndbId, RssType.Vndb);

            await UiThreadInvokeHelper.InvokeAsync(async Task() =>
            {
                if (game is null) //同步进来的游戏
                {
                    game = new Galgame();
                    gameService.AddVirtualGalgame(game);
                }

                ChangeProgress(index, changedGalgames.Count,
                    "PvnSyncTask_Downloading".GetLocalized(dto.id, dto.name ?? string.Empty));

                game.Ids[(int)RssType.PotatoVn] = dto.id.ToString();
                game.Ids[(int)RssType.Bangumi] = dto.bgmId ?? game.Ids[(int)RssType.Bangumi];
                game.Ids[(int)RssType.Vndb] = dto.vndbId ?? game.Ids[(int)RssType.Vndb];
                game.Name = dto.name ?? game.Name.Value ?? string.Empty;
                game.CnName = dto.cnName ?? game.CnName;
                game.Description = dto.description ?? game.Description.Value ?? string.Empty;
                game.Developer = dto.developer ?? game.Developer.Value ?? string.Empty;
                game.ExpectedPlayTime = dto.expectedPlayTime ?? game.ExpectedPlayTime.Value ?? string.Empty;
                game.Rating = dto.rating;
                if (dto.releasedDateTimeStamp is not null)
                    game.ReleaseDate = (dto.releasedDateTimeStamp ?? 0).ToDateTime();
                if (dto.imageUrl is not null)
                    game.ImagePath = await DownloadHelper.DownloadAndSaveImageAsync(dto.imageUrl, 0,
                        $"pvn_{dto.id}") ?? game.ImagePath.Value ?? Galgame.DefaultImagePath;

                if (dto.tags is not null)
                {
                    game.Tags.Value?.Clear();
                    dto.tags.ForEach(tag => game.Tags.Value?.Add(tag));
                }

                if (dto.playTime is not null)
                {
                    game.PlayedTime.Clear();
                    foreach (PlayLogDto time in dto.playTime)
                        game.PlayedTime[time.dateTimeStamp.ToDateTime().ToStringDefault()] = time.minute;
                    game.TotalPlayTime = game.PlayedTime.Values.Sum();
                }

                game.PlayType = dto.playType;
                game.Comment = dto.comment ?? game.Comment;
                game.MyRate = dto.myRate;
                game.PrivateComment = dto.privateComment;
                game.PvnUpdate = false;
            });
        }

        foreach (long id in deletedGalgames)
        {
            Galgame? game = gameService.GetGalgameFromId(id.ToString(), RssType.PotatoVn);
            if (game is null) continue;
            await gameService.RemoveGalgame(game, false);
        }

        await gameService.SaveGalgamesAsync();
        await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, latest);
    }

    private async Task CommitChanges(GalgameCollectionService gameService, IPvnService pvnService,
        IInfoService infoService,
        ILocalSettingsService settingsService)
    {
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
                    ChangeProgress(index, toUpdate.Count,
                        "PvnSyncTask_Uploading".GetLocalized(game.Name.Value!));
                    var id = await pvnService.UploadInternal(game);
                    game.Ids[(int)RssType.PotatoVn] = id.ToString();
                    await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, DateTime.Now.ToUnixTime());
                }
                catch (Exception e)
                {
                    infoService.Event(InfoBarSeverity.Warning, "PvnSyncTask_Error_Upload", e);
                    ignore.Add(game);
                }
            }
        } while (toUpdate.Count > 0);
    }
}