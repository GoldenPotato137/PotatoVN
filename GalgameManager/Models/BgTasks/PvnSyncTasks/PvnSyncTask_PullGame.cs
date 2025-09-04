using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using AutoMapper;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using PotatoVN.Client.Model;
using PlayType = GalgameManager.Enums.PlayType;

namespace GalgameManager.Models.BgTasks;

[SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
[SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
public class PvnSyncTaskPullGame(
    IGalgameCollectionService gameService,
    ILocalSettingsService settingsService,
    IInfoService infoService,
    IMapper mapper)
    : QueueTaskBase<GalgameDto>
{
    protected override int MaxRunning() => 5;
    public override string Title => "PvnSyncTask_PullGame_Title".GetLocalized();
    public string Result { get; private set; } = string.Empty;

    public void Init(IList<GalgameDto> games)
    {
        foreach (GalgameDto game in games)
            Queue.Enqueue(game);
        UpdateProgressMsg();
    }

    protected async override Task ProcessItemAsync(GalgameDto item)
    {
        Galgame? game = gameService.GetGalgameFromId(item.Id.ToString(), RssType.PotatoVn);
        await UiThreadInvokeHelper.InvokeAsync(async Task () =>
        {
            try
            {
                game ??= gameService.GetGalgameFromUid(new GalgameUid
                {
                    BangumiId = item.BgmId,
                    VndbId = item.VndbId,
                    Name = item.Name ?? string.Empty,
                    CnName = item.CnName,
                });

                if (game is null) //同步进来的游戏
                {
                    game = new Galgame();
                    gameService.AddVirtualGalgame(game);
                    Result += "PvnSyncTask_Pull_Added".GetLocalized(item.Name ?? string.Empty, item.Id) + "\n";
                }
                else
                    Result += "PvnSyncTask_Pull_Updated".GetLocalized(item.Name ?? string.Empty, item.Id) + "\n";

                game.Ids[(int)RssType.PotatoVn] = item.Id.ToString();
                game.Ids[(int)RssType.Bangumi] = item.BgmId ?? game.Ids[(int)RssType.Bangumi];
                game.Ids[(int)RssType.Vndb] = item.VndbId ?? game.Ids[(int)RssType.Vndb];
                game.UpdateMixedId();
                game.Name = item.Name ?? game.Name.Value ?? string.Empty;
                game.CnName = item.CnName ?? game.CnName;
                game.Description = item.Description ?? game.Description.Value ?? string.Empty;
                game.Developer = item.Developer ?? game.Developer.Value ?? string.Empty;
                game.ExpectedPlayTime = item.ExpectedPlayTime ?? game.ExpectedPlayTime.Value ?? string.Empty;
                game.Rating = item.Rating;
                game.ReleaseDate = item.ReleasedDateTimeStamp.ToDateTime().ToLocalTime();
                if (item.ImageUrl is not null)
                    game.ImagePath = await DownloadHelper.DownloadAndSaveImageWithDiffThread(item.ImageUrl, 0,
                        $"pvn_{item.Id}") ?? game.ImagePath.Value ?? Galgame.DefaultImagePath;
                        
                if (item.HeaderImageUrl is not null && await settingsService.ReadSettingAsync<bool>(KeyValues.SyncHeaderImage))
                {
                    game.AutoFetchStatus.HeaderImage = true;
                    var rawImagePath = await DownloadHelper.DownloadAndSaveImageWithDiffThread(item.HeaderImageUrl, 0,
                        $"pvn_{item.Id}_header_tmp");
                    if (rawImagePath is not null)
                    {
                        var finalHeaderPath = Path.Combine(
                            (await FileHelper.GetFolderAsync(FileHelper.FolderType.Images)).Path,
                            $"{item.Name ?? "unknown"}_Header.png".RemoveInvalidChars());
                        await Task.Run(() =>
                        {
                            if (!GetHeaderFromRssTask.ShouldProcess(item.HeaderImageUrl))
                                File.Move(rawImagePath, finalHeaderPath, true);
                            else
                            {
                                // Image needs processing, apply the same logic as GetHeaderFromRssTask
                                var fromSteam = item.HeaderImageUrl.Contains("cdn.akamai.steamstatic.com");
                                DownloadHelper.ProcessImage(rawImagePath, finalHeaderPath, !fromSteam);
                                if (File.Exists(rawImagePath)) File.Delete(rawImagePath);
                            }
                        });
                        if (Utils.IsImageValid(finalHeaderPath))
                        {
                            game.HeaderImagePath.Value = finalHeaderPath;
                            game.RaisePropertyChanged(nameof(game.HeaderImagePath));
                        }
                        game.HeaderImageUrl = item.HeaderImageUrl;
                    }
                }
                
                if (item.Tags is not null)
                {
                    game.Tags.Value?.Clear();
                    item.Tags.ForEach(tag => game.Tags.Value?.Add(tag));
                }

                if (item.Characters is not null
                    && item.CharacterLastChangedTimeStamp > game.PvnLastCharacterFetchTime
                    && await settingsService.ReadSettingAsync<bool>(KeyValues.SyncGameCharacters))
                {
                    UiThreadInvokeHelper.IgnoreComException(game.Characters.Clear);
                    List<Task<string?>> fetchImageTask = [], fetchPreviewImageTask = [];
                    foreach (CharacterDto c in item.Characters)
                    {
                        fetchImageTask.Add(DownloadHelper.DownloadAndSaveImageWithDiffThread(c.ImageUrl));
                        fetchPreviewImageTask.Add(
                            DownloadHelper.DownloadAndSaveImageWithDiffThread(c.PreviewImageUrl));
                    }

                    for (var i = 0; i < item.Characters.Count; i++)
                    {
                        GalgameCharacter newC = mapper.Map<GalgameCharacter>(item.Characters[i]);
                        newC.ImagePath = await fetchImageTask[i] ?? Galgame.DefaultImagePath;
                        newC.PreviewImagePath = await fetchPreviewImageTask[i] ?? Galgame.DefaultImagePath;
                        UiThreadInvokeHelper.IgnoreComException(game.Characters.Add, newC);
                    }

                    game.PvnLastCharacterFetchTime = DateTime.Now.ToUnixTime();
                }

                if (item.PlayTime is not null)
                {
                    Dictionary<string, int> playTime = new();
                    foreach (PlayLogDto time in item.PlayTime)
                        playTime[time.DateTimeStamp.ToDateTime().ToLocalTime().ToStringDefault()] = time.Minute;
                    game.MergeTime(new Galgame { PlayedTime = playTime });
                }

                game.PlayType = (PlayType)(int)(item.PlayType ?? 0);
                game.Comment = item.Comment ?? game.Comment;
                game.MyRate = item.MyRate;
                game.PrivateComment = item.PrivateComment;
                game.PvnUpdate = false;

                await gameService.SaveGalgameAsync(game);
            }
            catch (COMException)
            {
                infoService.DeveloperEvent(msg: "ComException on Sync Games");
            }
        });
    }

    protected override string ProgressTitle() => "PvnSyncTask_PullGame_Progress";

    protected override string ProgressMsg(GalgameDto item) => item.Name;

    protected override string ProgressWaitingMsg() => "PvnSyncTask_PullGame_Waiting";
}
