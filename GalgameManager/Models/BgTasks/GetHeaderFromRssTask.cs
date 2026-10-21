using System.Collections.Concurrent;
using GalgameManager.Contracts.BgTasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;

namespace GalgameManager.Models.BgTasks;

public class GetHeaderFromRssTask : QueueTaskBase<GetHeaderFromRssTask.HeaderRequest>, IGameProcessQueue
{
    /// <param name="Game">要获取横幅图的游戏</param>
    /// <param name="PvnBannerOnly">只尝试替换为PotatoVN banner，没有banner时保持原图</param>
    public record HeaderRequest(Galgame Game, bool PvnBannerOnly);

    /// <summary>
    /// PotatoVN自制banner图（无需裁切）的地址前缀，完整地址为{前缀}v{vndbId}.png，并非所有游戏都有
    /// </summary>
    private const string PvnBannerUrlPrefix = "https://statics.potatovn.net/banner/";
    /// 横幅图较大（PotatoVN banner约2MB），默认的10s超时在网络较慢时容易下载失败
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(60);
    private static readonly SemaphoreSlim PvnBannerMapLock = new(1, 1);
    /// 本次启动中已尝试过替换PotatoVN banner的游戏，避免没有banner的游戏每次打开详情页都请求一次
    private static readonly ConcurrentDictionary<Guid, byte> PvnBannerTriedGames = new();

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
        Queue.Enqueue(new HeaderRequest(game, false));
        UpdateProgressMsg();
    }

    /// <summary>
    /// 将游戏加入队列，只尝试把横幅图替换为PotatoVN banner，调用前应先用<see cref="NeedPvnBannerAsync"/>判断
    /// </summary>
    public void AddGalgameForPvnBanner(Galgame game)
    {
        Queue.Enqueue(new HeaderRequest(game, true));
        UpdateProgressMsg();
    }

    /// <summary>
    /// 打开游戏详情页时是否需要尝试将横幅图替换为PotatoVN banner：
    /// 启用了PotatoVN banner、不是steam游戏、横幅图未锁定、尚未替换过，且本次启动还没尝试过
    /// </summary>
    public static async Task<bool> NeedPvnBannerAsync(Galgame game, ILocalSettingsService settingsService)
    {
        if (!await settingsService.ReadSettingAsync<bool>(KeyValues.UsePvnBanner)) return false;
        if (game.HeaderImagePath.IsLock || IsSteamGame(game)) return false;
        Dictionary<Guid, bool>? replacedMap =
            await settingsService.ReadSettingAsync<Dictionary<Guid, bool>>(KeyValues.PvnBannerReplacedMap, isLarge: true);
        if (replacedMap?.GetValueOrDefault(game.Uuid) == true) return false;
        return PvnBannerTriedGames.TryAdd(game.Uuid, 0);
    }

    public override string Title => "GetHeaderFromRssTask_Title".GetLocalized();

    protected async override Task ProcessItemAsync(HeaderRequest request)
    {
        Galgame item = request.Game;
        if (item.HeaderImagePath.IsLock) return;
        List<Func<Task<string?>>> sources = [];
        if (request.PvnBannerOnly)
            sources.Add(GetPvnBannerUrlAsync);
        else
        {
            item.AutoFetchStatus.HeaderImage = true;
            await _gameService.SaveGalgameAsync(item);
            // 优先级：steam -> PotatoVN banner -> vndb截图，下载失败（如PotatoVN没有该游戏的banner，404）则顺延
            if (IsSteamGame(item)) sources.Add(() => _steamParser.GetGalHeaderAsync(item));
            if (await _settingsService.ReadSettingAsync<bool>(KeyValues.UsePvnBanner))
                sources.Add(GetPvnBannerUrlAsync);
            sources.Add(() => _vndbParser.GetGalHeaderAsync(item));
        }

        string? url = null, rawImage = null;
        foreach (Func<Task<string?>> source in sources)
        {
            url = await source();
            rawImage = await DownloadHelper.DownloadAndSaveImageWithDiffThread(url,
                fileNameWithoutExtension: $"{item.Name.Value?.RemoveInvalidChars() ?? string.Empty}_tmp",
                timeout: DownloadTimeout);
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
        if (url.StartsWith(PvnBannerUrlPrefix)) await MarkPvnBannerReplacedAsync(item.Uuid);
        if (await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncGames) &&
            await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncHeaderImage))
            _pvnService.Upload(item, PvnUploadProperties.HeaderImageLoc);
        return;

        async Task<string?> GetPvnBannerUrlAsync() =>
            await _vndbParser.GetVndbIdAsync(item) is { } vid ? $"{PvnBannerUrlPrefix}{vid}.png" : null;
    }

    protected override string ProgressTitle() => "GetHeaderFromRssTask_Progress_Title";

    protected override string ProgressMsg(HeaderRequest item) => item.Game.Name.Value ?? string.Empty;

    protected override string ProgressWaitingMsg() => "GetHeaderFromRssTask_Progress_Waiting";

    private async Task MarkPvnBannerReplacedAsync(Guid uuid)
    {
        await PvnBannerMapLock.WaitAsync();
        try
        {
            // 复制一份再修改保存，避免与NeedPvnBannerAsync同时读写同一个字典
            Dictionary<Guid, bool> replacedMap = new(
                await _settingsService.ReadSettingAsync<Dictionary<Guid, bool>>(KeyValues.PvnBannerReplacedMap,
                    isLarge: true) ?? []);
            replacedMap[uuid] = true;
            await _settingsService.SaveSettingAsync(KeyValues.PvnBannerReplacedMap, replacedMap, isLarge: true);
        }
        finally
        {
            PvnBannerMapLock.Release();
        }
    }

    private static bool IsSteamGame(Galgame game) =>
        !string.IsNullOrEmpty(game.Ids[(int)RssType.Steam]) && game.Ids[(int)RssType.Steam] != "-1";

    public static bool ShouldProcess(string url) =>
        url.Contains("cdn.akamai.steamstatic.com") || url.Contains("vndb.org") ||
        url.StartsWith(PvnBannerUrlPrefix);

    /// <summary>
    /// 只有vndb截图需要裁掉底部的对话框，steam图与PotatoVN banner不需要
    /// </summary>
    public static bool ShouldCutBottom(string url) => url.Contains("vndb.org");
}
