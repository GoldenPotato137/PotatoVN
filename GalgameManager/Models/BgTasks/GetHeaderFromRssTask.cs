using GalgameManager.Contracts.BgTasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;

namespace GalgameManager.Models.BgTasks;

public class GetHeaderFromRssTask : QueueTaskBase<Galgame>, IGameProcessQueue
{
    private readonly IGalgameCollectionService _gameService;
    private readonly VndbPhraser _vndbParser;
    private readonly SteamParser _steamParser;
    private readonly IPvnService _pvnService;
    private readonly ILocalSettingsService _settingsService;

    public GetHeaderFromRssTask(IGalgameCollectionService gameService, IPvnService pvnService,
        ILocalSettingsService settingsService)
    {
        _gameService = gameService;
        _vndbParser = (gameService.PhraserList[(int)RssType.Vndb] as VndbPhraser)!;
        _steamParser = (gameService.PhraserList[(int)RssType.Steam] as SteamParser)!;
        _pvnService = pvnService;
        _settingsService = settingsService;
    }

    public void AddGalgame(Galgame? game)
    {
        if (game is null) return;
        Queue.Enqueue(game);
        UpdateProgressMsg();
    }

    public override string Title => "GetHeaderFromRssTask_Title".GetLocalized();

    protected async override Task ProcessItemAsync(Galgame item)
    {
        if (item.HeaderImagePath.IsLock) return;
        item.AutoFetchStatus.HeaderImage = true;
        await _gameService.SaveGalgameAsync(item);
        // 优先级：steam -> PotatoVN banner -> vndb截图，下载失败（如PotatoVN没有该游戏的banner，404）则顺延
        List<Func<Task<string?>>> sources = [];
        if (!string.IsNullOrEmpty(item.Ids[(int)RssType.Steam]) && item.Ids[(int)RssType.Steam] != "-1")
            sources.Add(() => _steamParser.GetGalHeaderAsync(item));
        sources.Add(async () =>
            await _vndbParser.GetVndbIdAsync(item) is { } vid ? $"{PvnBannerUrlPrefix}{vid}.png" : null);
        sources.Add(() => _vndbParser.GetGalHeaderAsync(item));

        string? url = null, rawImage = null;
        foreach (Func<Task<string?>> source in sources)
        {
            url = await source();
            rawImage = await DownloadHelper.DownloadAndSaveImageWithDiffThread(url,
                fileNameWithoutExtension: $"{item.Name.Value?.RemoveInvalidChars() ?? string.Empty}_tmp");
            if (rawImage is not null) break;
        }

        if (url is null || rawImage is null) return;
        item.HeaderImageUrl = url;
        var targetPath = Path.Combine((await FileHelper.GetFolderAsync(FileHelper.FolderType.Images)).Path,
            $"{item.Name.Value}_Header_{DateTime.Now.ToUnixTime()}.png".RemoveInvalidChars());
        await DownloadHelper.ProcessImageAsync(rawImage, targetPath, ShouldCutBottom(url));
        var oldImg = item.HeaderImagePath.Value;
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            item.HeaderImagePath.Value = targetPath;
            item.RaisePropertyChanged(nameof(item.HeaderImagePath));
        });
        if (Utils.IsImageValid(oldImg)) File.Delete(oldImg!);
        await _gameService.SaveGalgameAsync(item);
        if (File.Exists(rawImage)) File.Delete(rawImage);
        if (await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncGames) &&
            await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncHeaderImage))
            _pvnService.Upload(item, PvnUploadProperties.HeaderImageLoc);
    }

    protected override string ProgressTitle() => "GetHeaderFromRssTask_Progress_Title";

    protected override string ProgressMsg(Galgame item) => item.Name.Value ?? string.Empty;

    protected override string ProgressWaitingMsg() => "GetHeaderFromRssTask_Progress_Waiting";

    /// <summary>
    /// PotatoVN自制banner图（无需裁切）的地址前缀，完整地址为{前缀}v{vndbId}.png，并非所有游戏都有
    /// </summary>
    private const string PvnBannerUrlPrefix = "https://statics.potatovn.net/banner/";

    public static bool ShouldProcess(string url) =>
        url.Contains("cdn.akamai.steamstatic.com") || url.Contains("vndb.org") ||
        url.StartsWith(PvnBannerUrlPrefix);

    /// <summary>
    /// 只有vndb截图需要裁掉底部的对话框，steam图与PotatoVN banner不需要
    /// </summary>
    public static bool ShouldCutBottom(string url) => url.Contains("vndb.org");
}
