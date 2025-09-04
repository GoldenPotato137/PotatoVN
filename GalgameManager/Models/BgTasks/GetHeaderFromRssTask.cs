using GalgameManager.Contracts.BgTasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;

namespace GalgameManager.Models.BgTasks;

public class GetHeaderFromRssTask : QueueTaskBase<Galgame>, IGameProcessQueue
{
    private static readonly IGalgameCollectionService GameService = App.GetService<IGalgameCollectionService>();
    private readonly VndbPhraser _vndbParser = (GameService.PhraserList[(int)RssType.Vndb] as VndbPhraser)!;
    private readonly SteamParser _steamParser = (GameService.PhraserList[(int)RssType.Steam] as SteamParser)!;
    private readonly IPvnService _pvnService = App.GetService<IPvnService>();
    private readonly ILocalSettingsService _settingsService = App.GetService<ILocalSettingsService>();

    public void AddGalgame(Galgame? game)
    {
        if (game is null) return;
        Queue.Enqueue(game);
        UpdateProgressMsg();
    }
    
    public override string Title => "GetHeaderFromRssTask_Title".GetLocalized();
    
    protected async override Task ProcessItemAsync(Galgame item)
    {
        item.AutoFetchStatus.HeaderImage = true;
        await GameService.SaveGalgameAsync(item);
        var fromSteam = !string.IsNullOrEmpty(item.Ids[(int)RssType.Steam]) && item.Ids[(int)RssType.Steam] != "-1";
        var url =  fromSteam?  
            await _steamParser.GetGalHeaderAsync(item):
            await _vndbParser.GetGalHeaderAsync(item);
        
        if (url is null) return;
        item.HeaderImageUrl = url;
        var targetPath = Path.Combine((await FileHelper.GetFolderAsync(FileHelper.FolderType.Images)).Path,
            $"{item.Name.Value}_Header_{DateTime.Now.ToUnixTime()}.png".RemoveInvalidChars());
        var rawImage = await DownloadHelper.DownloadAndSaveImageWithDiffThread(url,
            fileNameWithoutExtension: $"{item.Name.Value ?? string.Empty}_tmp");
        if (rawImage is null) return;
        DownloadHelper.ProcessImage(rawImage, targetPath, !fromSteam);
        var oldImg = item.HeaderImagePath.Value;
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            item.HeaderImagePath.Value = targetPath;
            item.RaisePropertyChanged(nameof(item.HeaderImagePath));
        });
        if (Utils.IsImageValid(oldImg)) File.Delete(oldImg!);
        await GameService.SaveGalgameAsync(item);
        if (File.Exists(rawImage)) File.Delete(rawImage);
        if (await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncGames) &&
            await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncHeaderImage))
            _pvnService.Upload(item, PvnUploadProperties.HeaderImageLoc);
    }

    protected override string ProgressTitle() => "GetHeaderFromRssTask_Progress_Title";

    protected override string ProgressMsg(Galgame item) => item.Name.Value ?? string.Empty;

    protected override string ProgressWaitingMsg() => "GetHeaderFromRssTask_Progress_Waiting";

    public static bool ShouldProcess(string url) =>
        url.Contains("cdn.akamai.steamstatic.com") || url.Contains("vndb.org");
}
