using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.BgTasks;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Views.Dialog;
using GalgameManager.WinApp.Base.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LiteDB;
using FileAttributes = System.IO.FileAttributes;

namespace GalgameManager.Services;

public partial class GalgameCollectionService : IGalgameCollectionService
{
    /// _galgames 无序, _displayGalgames有序，<br/>
    /// <b>所有对这个数组的操作均应该使用UI线程执行，以防出现COMException</b>
    private readonly ObservableCollection<Galgame> _galgames = [];
    // A removed game can still be referenced by an in-flight parser/image task. Keep a weak, instance-specific
    // tombstone so those tasks cannot upsert the deleted row again. The instance check also prevents an old task
    // from overwriting a newly-added game that happens to reuse the same UUID.
    private readonly ConditionalWeakTable<Galgame, RemovedGameMarker> _removedGameInstances = new();
    private readonly object _gamePersistenceLock = new();
    private static ILocalSettingsService LocalSettingsService { get; set; } = null!;
    private readonly IJumpListService _jumpListService;
    private readonly IInfoService _infoService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IGalgameSourceCollectionService _galSrcService;
    private ILiteCollection<Galgame> _dbSet = null!;
    private readonly IMessenger _bus;
    public event EventHandler<GalgameMutationEventArgs>? GalgameMutated;
    public event Action<Galgame>? GalgameDeletedEvent; //当有galgame删除时触发
    public event Action<Galgame>? MetaSavedEvent; //当有galgame元数据保存时触发
    public event Action? GalgameLoadedEvent; //当galgame列表加载完成时触发

    public Dictionary<int, IGalInfoPhraser> PhraserList
    {
        get;
    } = [];

    public GalgameCollectionService(ILocalSettingsService localSettingsService, IJumpListService jumpListService,
        IGalgameSourceCollectionService galgameSourceService, IInfoService infoService, IBgTaskService bgTaskService,
        IMessenger bus)
    {
        LocalSettingsService = localSettingsService;
        LocalSettingsService.OnSettingChanged += async (key, _) => await OnSettingChanged(key);
        _jumpListService = jumpListService;
        // _filterService.OnFilterChanged += () => UpdateDisplay(UpdateType.ApplyFilter);
        _infoService = infoService;
        _bgTaskService = bgTaskService;
        _galSrcService = galgameSourceService;
        _bus = bus;
        _bus.Register<PluginLoadArgs>(this, OnPluginLoaded);

        BgmPhraser bgmPhraser = new(GetBgmData().Result);
        VndbPhraser vndbPhraser = new(GetVndbData().Result);
        YmgalPhraser ymgalPhraser = new();
        CngalPhraser cngalPhraser = new();
        HikarinagiPhraser hikarinagiPhraser = new(bus);
        SteamParser steamParser = new(localSettingsService
            .ReadSettingAsync<LanguageEnum>(KeyValues.Language).Result.ToSteamApiString());
        MixedPhraser mixedPhraser = new(bgmPhraser, vndbPhraser, ymgalPhraser, steamParser, hikarinagiPhraser, GetMixData(), bus);
        PhraserList[(int)RssType.Bangumi] = bgmPhraser;
        PhraserList[(int)RssType.Vndb] = vndbPhraser;
        PhraserList[(int)RssType.Ymgal] = ymgalPhraser;
        PhraserList[(int)RssType.Cngal] = cngalPhraser;
        PhraserList[(int)RssType.Mixed] = mixedPhraser;
        PhraserList[(int)RssType.Steam] = steamParser;
        PhraserList[(int)RssType.Hikarinagi] = hikarinagiPhraser;
    }

    public async Task InitAsync()
    {
        _dbSet = LocalSettingsService.Database.GetCollection<Galgame>("galgame");
        await LoadGalgames();
        await _jumpListService.CheckJumpListAsync(_galgames);
        await Upgrade();
    }

    public async Task StartAsync()
    {
        LocalSettingStatus status = await LocalSettingsService.ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true)
            ?? new();
        if (!status.GalgameDetectedSavePath)
        {
            try
            {
                foreach (Galgame galgame in _galgames)
#pragma warning disable CS0618 // 类型或成员已过时
                    if (galgame.PreferredLocalInstallation is { } installation)
                    {
                        installation.LocalConfig ??= new LocalInstallationConfig();
                        installation.LocalConfig.DetectedSavePath =
                            GamePortablePath.Create(galgame.DetectedSavePosition, installation.Path);
                        if (installation.Source is not null) _galSrcService.Save(installation.Source);
                    }
#pragma warning restore CS0618 // 类型或成员已过时
            }
            catch (Exception)
            {
                //ignore
            }
            status.GalgameDetectedSavePath = true;
            await LocalSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
        }
    }

    /// <summary>
    /// 从设置中读取galgames
    /// </summary>
    private async Task LoadGalgames()
    {
        await ImportAsync();

        List<Galgame> galgames = [];
        await Task.Run(() =>
        {
            LocalSettingStatus status =
                LocalSettingsService.ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true).Result ?? new();
            if (status.GameLiteDbUpgrade)
                galgames = _dbSet.FindAll().ToList();
            else
                galgames = LocalSettingsService.ReadSettingAsync<List<Galgame>>(KeyValues.Galgames, true).Result ?? [];
        }); //用Task.Run运行，防止阻塞UI线程
        _galgames.SyncCollection(galgames);

        foreach (Galgame g in _galgames)
        {
            g.ErrorOccurred += e =>
                _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning, "GalgameEvent", e);
            // 数目增加
            if (g.Ids.Length < Galgame.PhraserNumber)
            {
                g.Ids = g.Ids.ResizeArray(Galgame.PhraserNumber);
            }
        }
        GalgameLoadedEvent?.Invoke();
    }

    /// <summary>
    /// 可能不同版本行为不同，需要对已存储的galgame进行升级
    /// </summary>
    private async Task Upgrade()
    {
        if (!await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.IdFromMixedUpgraded))
        {
            foreach (Galgame galgame in _galgames)
                galgame.UpdateIdFromMixed();
            await LocalSettingsService.SaveSettingAsync(KeyValues.IdFromMixedUpgraded, true);
        }

        if (!await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SavePathUpgraded))
        {
            _galgames.ToList().ForEach(galgame => galgame.FindSaveInPath());
            await LocalSettingsService.SaveSettingAsync(KeyValues.SavePathUpgraded, true);
        }

        // 给混合搜刮器设置的搜刮优先级添加新添加的搜刮器
        if (await LocalSettingsService.ReadSettingAsync<int>(KeyValues.MixedPhraserOrderVersion) !=
            MixedPhraserOrder.Version)
            await MixedPhraserOrderUpdate();

        // 游戏列表数据库化
        await UpgradeToLiteDb();
    }

    public async Task RemoveGalgame(Galgame galgame, bool removeFromDisk = false)
    {
        lock (_gamePersistenceLock)
        {
            if (!_galgames.Contains(galgame)) return;
            _removedGameInstances.GetValue(galgame, static _ => new RemovedGameMarker());
        }
        var removedFromCollection = false;
        try
        {
            if (removeFromDisk)
            {
                foreach (GalgameAndPath installation in galgame.LocalInstallations
                             .Where(e => e.Source is GalgameFolderSource).ToList())
                    await _galSrcService.MoveOutNoOperate(installation, true);
            }
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                try
                {
                    lock (_gamePersistenceLock)
                        removedFromCollection = _galgames.Remove(galgame);
                }
                catch (COMException)
                {
                    //框架bug：在试图更新UI界面的时候抛出异常，不影响逻辑正常运行
                    //暂时忽略
                }
            });
            lock (_gamePersistenceLock)
                _dbSet.Delete(galgame.Uuid);
            foreach (GalgameAndPath entry in galgame.SourceEntries.ToList())
                await _galSrcService.MoveOutNoOperate(entry);
            await UiThreadInvokeHelper.InvokeAsync(() => GalgameDeletedEvent?.Invoke(galgame));
        }
        catch
        {
            // If removal failed before the game left the collection, allow normal saves again.
            if (!removedFromCollection)
            {
                lock (_gamePersistenceLock)
                    _removedGameInstances.Remove(galgame);
            }
            throw;
        }
    }

    public Task<Galgame> ParseGalInfoAsync(Galgame galgame, RssType rssType = RssType.None,
        bool requireConfirm = false, GameParseType type = GameParseType.All) =>
        ParseGalInfoInternalAsync(galgame, rssType, requireConfirm, type, notify: true);

    private async Task<Galgame> ParseGalInfoInternalAsync(Galgame galgame, RssType rssType,
        bool requireConfirm, GameParseType type, bool notify)
    {
        if (!IsCurrentGameInstance(galgame))
            throw new PvnException($"Game {galgame.Name.Value} is not in game list");
        RssType selectedRss = rssType;
        if(selectedRss == RssType.None)
            selectedRss = galgame.RssType == RssType.None ? await LocalSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType) : galgame.RssType;
        Galgame result = galgame;
        if (type.HasFlag(GameParseType.GameInfo) || type.HasFlag(GameParseType.Character) || type.HasFlag(GameParseType.Image))
            result = await ParseAsync(galgame, PhraserList[(int)selectedRss], type);
        if (requireConfirm)
        {
            ConfirmGalInfoDialog dialog = new(galgame, result, this);
            ContentDialogResult tmp = await dialog.ShowAsync();
            if (tmp == ContentDialogResult.Secondary)
                throw new PvnException("Canceled".GetLocalized());
        }

        if (type.HasFlag(GameParseType.PlayStatus) &&
            await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SyncPlayStatusWhenPhrasing))
        {
            // 优先Bgm
            await DownLoadPlayStatusAsync(galgame, RssType.Vndb);
            await DownLoadPlayStatusAsync(galgame, RssType.Bangumi);
        }
        await SaveGalgameAsync(galgame);
        if (type.HasFlag(GameParseType.Character) &&
            LocalSettingsService.ReadSettingAsync<bool>(KeyValues.DownloadCharacters).Result)
            AddGameToBgTask<GetGalgameCharactersFromRssTask>();
        if (type.HasFlag(GameParseType.HeaderImage))
            AddGameToBgTask<GetHeaderFromRssTask>();
        if (notify && IsCurrentGameInstance(galgame))
            await RaiseGalgameMutatedAsync(new GalgameMutationEventArgs(galgame, GetChangeKind(type),
                GalgameChangeOrigin.Parser, type));
        return result;

        void AddGameToBgTask<TBgTask>() where TBgTask : BgTaskBase, IGameProcessQueue
        {
            if (!IsCurrentGameInstance(galgame)) return;
            var isNew = false;
            TBgTask? task = _bgTaskService.GetBgTask<TBgTask>(string.Empty);
            if (task is null)
            {
                task = _bgTaskService.CreateBgTask<TBgTask>();
                isNew = true;
            }
            task.AddGalgame(galgame);
            if (isNew) _ = _bgTaskService.AddBgTask(task);
        }
    }

    private static GalgameChangeKind GetChangeKind(GameParseType type)
    {
        GalgameChangeKind result = GalgameChangeKind.None;
        if (type.HasFlag(GameParseType.GameInfo)) result |= GalgameChangeKind.Metadata;
        if (type.HasFlag(GameParseType.Image) || type.HasFlag(GameParseType.HeaderImage))
            result |= GalgameChangeKind.Images;
        if (type.HasFlag(GameParseType.Character)) result |= GalgameChangeKind.Characters;
        if (type.HasFlag(GameParseType.PlayStatus)) result |= GalgameChangeKind.PlayStatus;
        return result;
    }

    private Task RaiseGalgameMutatedAsync(GalgameMutationEventArgs args) =>
        UiThreadInvokeHelper.InvokeAsync(() => RaiseGalgameMutated(args));

    private void RaiseGalgameMutated(GalgameMutationEventArgs args)
    {
        EventHandler<GalgameMutationEventArgs>? handlers = GalgameMutated;
        if (handlers is null) return;
        foreach (Delegate invocation in handlers.GetInvocationList())
        {
            if (invocation is not EventHandler<GalgameMutationEventArgs> handler) continue;
            try
            {
                handler(this, args);
            }
            catch (Exception e)
            {
                _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                    "Failed On Calling GalgameMutated", e);
            }
        }
    }

    public async Task<Galgame> ParseGalInfoOnlyAsync(Galgame galgame, RssType rssType = RssType.None, bool requireConfirm = false)
    {
        RssType selectedRss = rssType;
        if (selectedRss == RssType.None)
            selectedRss = galgame.RssType == RssType.None
                ? LocalSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType).Result
                : galgame.RssType;
        Galgame result = await ParseAsync(galgame, PhraserList[(int)selectedRss], GameParseType.All);
        if (requireConfirm)
        {
            ConfirmGalInfoDialog dialog = new(galgame, result, this);
            ContentDialogResult tmp = await dialog.ShowAsync();
            if (tmp == ContentDialogResult.Secondary)
                throw new PvnException("Canceled".GetLocalized());
        }
        return result;
    }

    public async Task ExportAsync(Action<string, int, int>? progress)
    {
        ObservableCollection<Galgame> tmp = new(_galgames.Select(g => g.DeepClone()));
        for(var i = 0; i < tmp.Count; i++)
        {
            Galgame game = tmp[i];
            progress?.Invoke("GalgameCollectionService_Export_Progress".GetLocalized(game.Name.Value ?? string.Empty),
                i + 1, tmp.Count);
            if (Utils.IsImageValid(game.ImagePath.Value))
                game.ImagePath.ForceSet(await LocalSettingsService.AddImageToExportAsync(game.ImagePath.Value) ??
                                        Galgame.DefaultImagePath);
            if (Utils.IsImageValid(game.HeaderImagePath.Value))
                game.HeaderImagePath.ForceSet(await LocalSettingsService.AddImageToExportAsync(game.HeaderImagePath.Value));
            foreach (GalgameCharacter character in game.Characters)
            {
                if (Utils.IsImageValid(character.ImagePath))
                    character.ImagePath = await LocalSettingsService.AddImageToExportAsync(character.ImagePath) ??
                                          Galgame.DefaultCharacterImagePath;
                if (Utils.IsImageValid(character.PreviewImagePath))
                    character.PreviewImagePath =
                        await LocalSettingsService.AddImageToExportAsync(character.PreviewImagePath) ??
                        Galgame.DefaultCharacterImagePath;
            }
        }
        await LocalSettingsService.AddToExportAsync(KeyValues.Galgames, tmp);
    }

    /// <inheritdoc />
    public async Task<GalgameCharacter> PhraseGalCharacterAsync(GalgameCharacter galgameCharacter,
        RssType rssType = RssType.None, Guid? gameUuid = null)
    {
        gameUuid ??= _galgames.FirstOrDefault(g => g.Characters.Contains(galgameCharacter))?.Uuid;
        // 插件可能解析尚未加入游戏的角色，此时使用临时图片命名空间，避免覆盖已有图片。
        return await PhraserCharacterAsync(galgameCharacter, PhraserList[(int)rssType], gameUuid ?? Guid.NewGuid());
    }

    public async Task<List<string>> ParserGalImagesAsync(Galgame galgame, GameParseType parseType)
    {
        List<Task<List<string>>> tasks = [];
        foreach ((int rssTypeValue, IGalInfoPhraser phraser) in PhraserList.ToList())
        {
            RssType rssType = (RssType)rssTypeValue;
            string? parserId = rssTypeValue >= 100
                ? galgame.IdForPlugins.GetValueOrDefault(rssTypeValue)
                : rssTypeValue < galgame.Ids.Length ? galgame.Ids[rssTypeValue] : null;
            if (parserId == "-1") continue;
            Galgame game = new()
            {
                RssType = rssType,
                Ids = (string?[])galgame.Ids.Clone(),
                IdForPlugins = galgame.IdForPlugins.ToDictionary(),
            };
            game.Name.Value = galgame.Name.Value;

            if (parseType == GameParseType.HeaderImage)
            {
                if (phraser is IGalHeadersParser headerParser)
                {
                    tasks.Add(Task.Run(async () => await headerParser.GetGalHeadersAsync(game)));
                }
            }
            else if (parseType == GameParseType.Image)
            {
                if (phraser is IGalCoversParser coverParser)
                {
                    if (phraser is MixedPhraser) continue; // 混合搜刮器是所有搜刮器并集的真子集，没必要再调用一次
                    tasks.Add(Task.Run(async () => await coverParser.GetGalCoversAsync(game)));
                }
            }
            else
            {
                throw new ArgumentException("Unsupported GameParseType for ParserGalImagesAsync");
            }
        }

        var safeTasks = tasks.Select(async t =>
        {
            try
            {
                return await t;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        });

        var results = await Task.WhenAll(safeTasks);

        List<string> imageUrls = new();
        foreach (List<string> images in results)
        {
            if (images != null)
            {
                imageUrls.AddRange(images.Where(url => !string.IsNullOrEmpty(url)));
            }
        }

        return imageUrls.Distinct().ToList();
    }

    /// <summary>
    /// 获取角色信息，并下载按游戏隔离的角色图片。
    /// </summary>
    /// <param name="galgameCharacter">接收角色信息和本地图片路径的角色对象</param>
    /// <param name="phraser">提供角色信息的搜刮器</param>
    /// <param name="gameUuid">用于隔离图片文件名的游戏UUID，或未关联游戏时使用的临时图片命名空间</param>
    /// <returns>更新后的角色对象；搜刮器不支持角色或未找到信息时返回原对象</returns>
    private static async Task<GalgameCharacter> PhraserCharacterAsync(GalgameCharacter galgameCharacter,
        IGalInfoPhraser phraser, Guid gameUuid)
    {
        if (phraser is not IGalCharacterPhraser characterPhraser) return galgameCharacter;
        GalgameCharacter? tmp = await characterPhraser.GetGalgameCharacter(galgameCharacter);
        if (tmp == null) return galgameCharacter;
        galgameCharacter.Name = tmp.Name;
        galgameCharacter.Summary = tmp.Summary;
        galgameCharacter.Gender = tmp.Gender;
        galgameCharacter.BirthDay = tmp.BirthDay;
        galgameCharacter.BirthMon = tmp.BirthMon;
        galgameCharacter.BirthYear = tmp.BirthYear;
        galgameCharacter.BirthDate = tmp.BirthDate;
        galgameCharacter.BloodType = tmp.BloodType;
        galgameCharacter.Height = tmp.Height;
        galgameCharacter.Weight = tmp.Weight;
        galgameCharacter.BWH = tmp.BWH;

        HttpClient? client = (phraser as IHttpClientProvider)?.HttpClient;
        galgameCharacter.ImagePath = await DownloadHelper.DownloadAndSaveImageWithDiffThread(tmp.ImageUrl,
            fileNameWithoutExtension: DownloadHelper.GetCharacterImageFileName(gameUuid, galgameCharacter.Name),
            client: client) ?? Galgame.DefaultCharacterImagePath;
        galgameCharacter.PreviewImagePath = await DownloadHelper.DownloadAndSaveImageWithDiffThread(tmp.PreviewImageUrl,
            fileNameWithoutExtension: DownloadHelper.GetCharacterImageFileName(gameUuid, galgameCharacter.Name, preview: true)) ??
            Galgame.DefaultCharacterImagePath;
        return galgameCharacter;
    }

    private async Task<Galgame> ParseAsync(Galgame galgame, IGalInfoPhraser phraser, GameParseType type)
    {
        _bus.Send(new GalgameParsingEventArgs(galgame, "GalgameCollectionService_ParseAsync_WaitingParser".GetLocalized()));
        Galgame? tmp = await phraser.GetGalgameInfo(galgame);
        if (tmp == null) return galgame;

        await UiThreadInvokeHelper.InvokeAsync(async () =>
        {
            galgame.RssType = phraser.GetPhraseType();
            galgame.Id = tmp.Id;
            if (type.HasFlag(GameParseType.GameInfo))
            {
                galgame.Description.Value = tmp.Description.Value;
                if (tmp.Description != Galgame.DefaultString)
                    galgame.Description.Value = tmp.Description.Value;
                if (tmp.Developer != Galgame.DefaultString)
                    galgame.Developer.Value = tmp.Developer.Value;
                if (tmp.Engine != Galgame.DefaultString && !string.IsNullOrEmpty(tmp.Engine.Value))
                    galgame.Engine.Value = tmp.Engine.Value;
                if (tmp.ExpectedPlayTime != Galgame.DefaultString)
                    galgame.ExpectedPlayTime.Value = tmp.ExpectedPlayTime.Value;
                switch (await LocalSettingsService.ReadSettingAsync<DisplayName>(KeyValues.DefaultGameName))
                {
                    case DisplayName.Name:
                        break;
                    case DisplayName.ChineseName:
                        galgame.Name.Value = !string.IsNullOrEmpty(tmp.CnName) ? tmp.CnName : tmp.Name.Value;
                        break;
                    case DisplayName.OriginalName:
                        galgame.Name.Value = tmp.Name.Value;
                        break;
                }
                galgame.ChineseName.Value = tmp.CnName;
                galgame.OriginalName.Value = tmp.Name.Value ?? string.Empty;
                galgame.Rating.Value = tmp.Rating.Value;
                if (!galgame.Tags.IsLock && tmp.Tags.Value?.Count > 0) // Tags不能直接赋值，直接替换容器会抛出奇怪的绑定异常
                {
                    try
                    {
                        galgame.Tags.Value ??= new ObservableCollection<string>(); //不应该发生
                        galgame.Tags.Value.SyncCollection(tmp.Tags.Value);
                    }
                    catch (COMException)
                    {
                        //可能会在某些界面触发ComException（怀疑是框架bug），但不影响正常赋值，暂时忽略
                    }
                }
                galgame.ReleaseDate.Value = tmp.ReleaseDate.Value;
            }
            if (type.HasFlag(GameParseType.Character))
                galgame.Characters = tmp.Characters;
            if (type.HasFlag(GameParseType.Image))
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                HttpClient? client = phraser is IHttpClientProvider provider ? provider.HttpClient : null;
                await Task.Delay(20);
                _bus.Send(new GalgameParsingEventArgs(galgame, "GalgameCollectionService_ParseAsync_GettingImg".GetLocalized()));
                galgame.ImageUrl = tmp.ImageUrl;
                var oldImg = galgame.ImagePath.Value;
                var newImg = await DownloadHelper.DownloadAndSaveImageWithDiffThread(galgame.ImageUrl,
                    fileNameWithoutExtension: $"{galgame.Name.Value ?? string.Empty}_{DateTime.Now.ToUnixTime()}_cover",
                    client: client);
                for (var i = 0; i < tmp.AlternateImageUrls.Count && string.IsNullOrEmpty(newImg); i++)
                    newImg = await DownloadHelper.DownloadAndSaveImageWithDiffThread(tmp.AlternateImageUrls[i],
                        fileNameWithoutExtension: $"{galgame.Name.Value ?? string.Empty}_{DateTime.Now.ToUnixTime()}_cover",
                        client: client);
                galgame.ImagePath.Value = newImg ?? oldImg;
                if (File.Exists(oldImg) && oldImg != galgame.ImagePath.Value) File.Delete(oldImg);
            }
            galgame.LastFetchInfoTime = DateTime.Now;
        });
        return galgame;
    }

    /// <summary>
    /// 下载某个游戏的游玩状态
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <param name="source">下载源</param>
    /// <returns>(下载结果，结果解释)</returns>
    public async Task<(GalStatusSyncResult, string)> DownLoadPlayStatusAsync(Galgame galgame, RssType source)
    {
        if (PhraserList[(int)source] is IGalStatusSync galStatusSync)
            return await galStatusSync.DownloadAsync(galgame);
        return (GalStatusSyncResult.Other, "这个数据源不支持同步游玩状态");
    }

    /// <summary>
    /// 从某个信息源下载所有游戏的游玩状态
    /// </summary>
    /// <param name="source">信息源</param>
    /// <returns>(结果，结果解释)</returns>
    public async Task<(GalStatusSyncResult ,string)> DownloadAllPlayStatus(RssType source)
    {
        var msg = string.Empty;
        GalStatusSyncResult result = GalStatusSyncResult.Other;
        IGalInfoPhraser phraser = PhraserList[(int)source];
        if (phraser is IGalStatusSync sync)
            (result, msg) = await sync.DownloadAllAsync(_galgames);
        await SaveGalgamesAsync();
        return (result, msg);
    }

    /// <summary>
    /// 刷新显示列表
    /// </summary>
    public void RefreshDisplay()
    {
    }

    /// <summary>
    /// 向信息源上传游玩状态
    /// </summary>
    /// <param name="galgame">要同步的游戏</param>
    /// <param name="rssType">信息源</param>
    /// <returns>(上传结果， 结果解释)</returns>
    /// <exception cref="NotSupportedException">若信息源没有实现IGalStatusSync，则抛此异常</exception>
    public async Task<(GalStatusSyncResult, string)> UploadPlayStatusAsync(Galgame galgame, RssType rssType)
    {
        IGalInfoPhraser phraser = PhraserList[(int)rssType];
        if (phraser is IGalStatusSync syncer)
            return await syncer.UploadAsync(galgame);
        throw new NotSupportedException("这个数据源不支持同步游玩状态");
    }

    /// <summary>
    /// 获取所有galgame
    /// </summary>
    public ObservableCollection<Galgame> Galgames => _galgames;

    /// <summary>
    /// 获取搜索建议
    /// </summary>
    /// <param name="current">当前文本串</param>
    /// <param name="searchName">是否包括游戏名的搜索建议</param>
    /// <param name="searchDeveloper">是否包括开发商搜索建议</param>
    /// <param name="searchTag">是否包括Tag搜索建议</param>
    /// <param name="searchChineseName">是否包括中文名搜索建议</param>
    /// <param name="searchOriginalName">是否包括游戏原名搜索建议</param>
    /// <returns>搜索建议，若没有则返回空List</returns>
    public async Task<List<string>> GetSearchSuggestions(string current, bool searchName = true,
        bool searchDeveloper = true, bool searchTag = true, bool searchChineseName = true, bool searchOriginalName = true)
    {
        List<string> tmp = new();
        await Task.Run(() =>
        {
            if (searchName) //Name
                tmp.AddRange(from galgame in _galgames
                    where galgame.Name.Value is not null && galgame.Name.Value.ContainX(current)
                    select galgame.Name.Value);
            if (searchDeveloper) //Developer
                tmp.AddRange(from galgame in _galgames
                    where galgame.Developer.Value is not null && galgame.Developer.Value.ContainX(current)
                    select galgame.Developer.Value);
            if (searchTag) //Tag
                tmp.AddRange(from galgame in _galgames
                    from tag in galgame.Tags.Value ?? new ObservableCollection<string>()
                    where tag.ContainX(current)
                    select tag);
            if (searchChineseName) //ChineseName
                tmp.AddRange(from galgame in _galgames
                    where galgame.ChineseName.Value is not null && galgame.ChineseName.Value.ContainX(current)
                    select galgame.ChineseName.Value);
            if (searchOriginalName) //OriginalName
                tmp.AddRange(from galgame in _galgames
                    where galgame.OriginalName.Value is not null && galgame.OriginalName.Value.ContainX(current)
                    select galgame.OriginalName.Value);
        });
        //去重
        tmp.Sort((a,b)=> a.CompareX(b));
        return tmp.Where((t, i) => i == 0 || t.CompareX(tmp[i - 1]) !=0).ToList();
    }

    public Galgame? GetGalgameFromUid(GalgameUid? uid, GalgameUidFetchMode mode = GalgameUidFetchMode.Same)
    {
        if (uid is null) return null;
        if (mode == GalgameUidFetchMode.Same)
            return _galgames.FirstOrDefault(g => g.Uid.IsSame(uid));
        if (mode == GalgameUidFetchMode.MaxSimilarity)
        {
            var max = 0;
            Galgame? result = null;
            foreach(Galgame g in _galgames)
                if (g.Uid.Similarity(uid) > max)
                {
                    result = g;
                    max = g.Uid.Similarity(uid);
                }
            return result;
        }
        return null;
    }

    public Galgame? GetGalgameFromUuid(Guid? uuid)
    {
        if (uuid is null) return null;
        return _galgames.FirstOrDefault(g => g.Uuid == uuid);
    }

    public Galgame? GetGalgameFromId(string? id, RssType rssType)
    {
        if (id is null) return null;
        return _galgames.FirstOrDefault(g => g.Ids[(int)rssType] == id);
    }

    public Galgame? GetGalgameFromName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return _galgames.FirstOrDefault(g => g.Name.Value == name);
    }

    public Task SaveGalgamesAsync()
    {
        return Task.Run(() =>
        {
            lock (_gamePersistenceLock)
            {
                List<Galgame> gamesToSave = _galgames
                    .Where(CanPersistGameLocked)
                    .ToList();
                _dbSet.Upsert(gamesToSave);
            }
        });
    }

    public async Task SaveGalgameAsync(Galgame galgame)
    {
        lock (_gamePersistenceLock)
        {
            if (!CanPersistGameLocked(galgame)) return;
            _dbSet.Upsert(galgame);
        }
        await SaveMetaAsync(galgame);
    }

    private bool IsCurrentGameInstance(Galgame galgame)
    {
        lock (_gamePersistenceLock)
        {
            if (_removedGameInstances.TryGetValue(galgame, out _)) return false;
            return _galgames.Any(current => ReferenceEquals(current, galgame));
        }
    }

    private bool CanPersistGameLocked(Galgame galgame)
    {
        if (_removedGameInstances.TryGetValue(galgame, out _)) return false;
        Galgame? current = _galgames.FirstOrDefault(item => item.Uuid == galgame.Uuid);
        return current is null || ReferenceEquals(current, galgame);
    }

    private sealed class RemovedGameMarker;

    public Task SaveGalgameMetaAsync(Galgame galgame, GalgameSourceBase? targetSource = null)
    {
        if (targetSource is null) return SaveMetaAsync(galgame);
        if (!targetSource.Contain(galgame))
            throw new PvnException($"{targetSource.Name} does not contain {galgame.Name.Value}");
        return SourceServiceFactory.GetSourceService(targetSource.SourceType).SaveMetaAsync(galgame, targetSource);
    }

    /// <summary>
    /// 保存galgame的信息备份（包括meta.json和封面图）
    /// </summary>
    /// <param name="galgame"></param>
    private async Task SaveMetaAsync(Galgame galgame)
    {
        IEnumerable<GalgameSourceType> types = galgame.Sources.Select(s => s.SourceType)
            .Where(t => t != GalgameSourceType.Virtual).Distinct();
        List<(Task, GalgameSourceType)> tasks = new();
        foreach (GalgameSourceType type in types)
            tasks.Add((SourceServiceFactory.GetSourceService(type).SaveMetaAsync(galgame), type));
        foreach ((Task, GalgameSourceType) t in tasks)
        {
            try
            {
                await t.Item1;
            }
            catch (Exception e)
            {
                _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                    "GalgameCollectionService_BackupMetaFailed".GetLocalized(galgame.Name.Value
                                                                             ?? string.Empty, t.Item2.ToString()), e);
            }
        }
    }

    /// <summary>
    /// 保存所有galgame的信息备份（包括meta.json和封面图）
    /// </summary>
    public async Task SaveAllMetaAsync()
    {
        foreach (Galgame galgame in _galgames)
        {
            MetaSavedEvent?.Invoke(galgame);
            await SaveMetaAsync(galgame);
        }
    }

    /// <summary>
    /// 获取galgame的存档文件夹
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <param name="installation">用于解析相对存档路径的安装实例</param>
    /// <returns>存档文件夹地址，若用户取消返回null</returns>
    private async Task<string?> GetGalgameSaveAsync(Galgame galgame, GalgameAndPath installation)
    {
        // 几个可能的存档位置：
        // 1. SuggestedSavePath（由云端同步过来的路径）
        //      TODO(kuriko): 这部分之后改成 ISaveProvider，由插件提供
        // 2. DetectedSavePath（运行检测到的路径）
        // 3. 游戏根目录

        var localPath = installation.Path;
        LocalInstallationConfig config = installation.LocalConfig ??= new LocalInstallationConfig();
        GamePortablePath? detectedSavePath = config.DetectedSavePath;

        List<string> candidateSavePath = new();
        if (detectedSavePath?.ToPath() is { } path2) candidateSavePath.Add(path2);

        async Task<string?> ChooseFolder()
        {
            List<string> subFolders = galgame.GetSubFolders(installation);

            var isSuggestedSavePathFound = candidateSavePath.Count > 0;
            foreach (var suggestedPath in Enumerable.Reverse(candidateSavePath))
            {
                subFolders.RemoveAll(f => f.Equals(suggestedPath, StringComparison.OrdinalIgnoreCase));
                subFolders.Insert(0, suggestedPath);
            }

            var startupPath = isSuggestedSavePathFound
                ? Path.GetDirectoryName(candidateSavePath[0])
                : localPath;

            FolderOrFilePickerDialog dialog = new(
                App.MainWindow!.Content.XamlRoot,
                "GalgameCollectionService_SelectSavePosition_Folder".GetLocalized(),
                subFolders,
                isFolder: true,
                suggestedPath: startupPath,
                isFirstItemSuggested: isSuggestedSavePathFound);
            return await dialog.ShowAndAwaitResultAsync();
        }

        async Task<string?> ChooseFile()
        {
            List<string> rootFiles = galgame.GetRootFiles(installation);
            FolderOrFilePickerDialog dialog = new(
                App.MainWindow!.Content.XamlRoot,
                "GalgameCollectionService_SelectSavePosition_File".GetLocalized(),
                rootFiles,
                isFolder: false,
                suggestedPath: detectedSavePath?.ToPath() ?? localPath);
            return await dialog.ShowAndAwaitResultAsync();
        }

        // 如果检测到的话，优先用这个
        if (detectedSavePath?.ToPath()?.IsNullOrWhiteSpace() is false)
        {
            var result = await ChooseFolder();
            // 重置检测存档位置，允许重新显示选择单文件。
            if (result.IsNullOrWhiteSpace()) config.DetectedSavePath = null;
            return result;
        }

        ContentDialog folderOrFileSelector = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "GalgameCollectionService_SelectSaveType_Title".GetLocalized(),
            Content = "GalgameCollectionService_SelectSaveType_Content".GetLocalized(),
            PrimaryButtonText = "GalgameCollectionService_SelectSaveType_Folder".GetLocalized(),
            SecondaryButtonText = "GalgameCollectionService_SelectSaveType_File".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            CloseButtonText = "Cancel".GetLocalized(),
        };
        var typeResult = await folderOrFileSelector.ShowAsync();

        switch (typeResult)
        {
            case ContentDialogResult.Primary: // Folder
                return await ChooseFolder();
            case ContentDialogResult.Secondary: // File
                return await ChooseFile();
        }

        return null;
    }

    /// <summary>
    /// 获取并设置指定安装实例的可执行文件。
    /// </summary>
    /// <param name="galgame">逻辑游戏</param>
    /// <param name="installation">目标安装实例</param>
    /// <returns>可执行文件地址，如果用户取消或找不到可执行文件则返回null</returns>
    public async Task<string?> GetGalgameExeAsync(Galgame galgame, GalgameAndPath installation)
    {
        if (!installation.IsLocalInstallation || !Directory.Exists(installation.Path)) return null;
        installation.LocalConfig ??= new LocalInstallationConfig();
        List<string> exes = galgame.GetExesAndBats(installation);
        switch (exes.Count)
        {
            case 0:
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = App.MainWindow!.Content.XamlRoot,
                    RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
                    Title = "Error".GetLocalized(),
                    Content = "GalgameCollectionService_NotExeFounded".GetLocalized(),
                    PrimaryButtonText = "Yes".GetLocalized()
                };
                await dialog.ShowAsync();
                return null;
            }
            case 1:
                installation.LocalConfig.ExePath = exes[0];
                break;
            default:
            {
                SelectFileDialog dialog = new(installation.Path, new[] {".exe", ".bat", ".lnk"},
                    "GalgameCollectionService_SelectExe".GetLocalized(), false);
                await dialog.ShowAsync();
                if (dialog.SelectedFilePath == null) return null;
                installation.LocalConfig.ExePath = dialog.SelectedFilePath;
                break;
            }
        }
        if (installation.Source is not null) _galSrcService.Save(installation.Source);
        return installation.LocalConfig.ExePath;
    }

    /// <summary>
    /// 转换指定安装实例的存档位置。
    /// </summary>
    /// <param name="galgame">逻辑游戏</param>
    /// <param name="installation">目标安装实例</param>
    public async Task ChangeGalgameSavePosition(Galgame galgame, GalgameAndPath installation)
    {
        LocalInstallationConfig config = installation.LocalConfig ??= new LocalInstallationConfig();
        if (config.SavePath is not null && new DirectoryInfo(config.SavePath).Exists == false)
            await UiThreadInvokeHelper.InvokeAsync(() => config.SavePath = null);

        if (config.SavePath is not null && FolderOperations.IsSymbolicLink(config.SavePath)) //目前在云端
        {
            await Task.Run(() =>
            {
                FolderOperations.ConvertSymbolicLinkToActual(config.SavePath);
            });
            await UiThreadInvokeHelper.InvokeAsync(() => config.SavePath = null);
        }
        else //目前在本地
        {
            var remoteRoot = await LocalSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder);
            if (string.IsNullOrEmpty(remoteRoot))
            {
                _infoService.Info(InfoBarSeverity.Error, msg:"GalgameCollectionService_CloudRootNotSet".GetLocalized());
                return;
            }
            var localSavePath = await GetGalgameSaveAsync(galgame, installation);
            if (localSavePath == null) return;
            if (Utils.ArePathsEqual(remoteRoot, localSavePath))
                throw new PvnException("GalgameCollectionService_SavePathIsCloudRoot".GetLocalized());
            if (Utils.IsPathContained(localSavePath, installation.Path))
                throw new PvnException("GalgameCollectionService_SavePathIsGameRoot".GetLocalized());
            if (FolderOperations.IsSymbolicLink(localSavePath))
            {
                _infoService.Info(InfoBarSeverity.Warning, msg:"GalgameCollectionService_SavePathIsSymbolicLink".GetLocalized());
                await UiThreadInvokeHelper.InvokeAsync(() => config.SavePath = localSavePath);
                await SaveGalgameAsync(galgame);
                if (installation.Source is not null) _galSrcService.Save(installation.Source);
                return;
            }

            var tmp = localSavePath[..localSavePath.LastIndexOf('\\')];
            var target = tmp[tmp.LastIndexOf('\\')..] + localSavePath[localSavePath.LastIndexOf('\\')..];
            remoteRoot += target;
            try
            {
                if (new DirectoryInfo(remoteRoot).Exists) //云端已存在同名文件夹
                {
                    var choose = 0;
                    ContentDialog dialog = new()
                    {
                        XamlRoot = App.MainWindow!.Content.XamlRoot,
                        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
                        Title = "GalgameCollectionService_SelectOperateTitle".GetLocalized(),
                        Content = "GalgameCollectionService_SelectOperateMsg".GetLocalized(),
                        PrimaryButtonText = "GalgameCollectionService_Local".GetLocalized(),
                        SecondaryButtonText = "GalgameCollectionService_Cloud".GetLocalized(),
                        CloseButtonText = "Cancel".GetLocalized()
                    };
                    dialog.PrimaryButtonClick += (_, _) => choose = 1;
                    dialog.SecondaryButtonClick += (_, _) => choose = 2;
                    await dialog.ShowAsync();
                    if (choose == 1)
                    {
                        new DirectoryInfo(remoteRoot).Delete(true); //删除云端文件夹
                        FolderOperations.ConvertFolderOrFileToSymbolicLink(localSavePath, remoteRoot);
                    }
                    else if (choose == 2)
                    {
                        FolderOperations.Delete(localSavePath); //删除本地文件夹
                        FolderOperations.CreateSymbolicLink(localSavePath, remoteRoot);
                    }
                }
                else
                    FolderOperations.ConvertFolderOrFileToSymbolicLink(localSavePath, remoteRoot);

                await UiThreadInvokeHelper.InvokeAsync(() => config.SavePath = localSavePath);
            }
            catch (Exception e) //创建符号链接失败，把存档复制回去
            {
                if(Directory.Exists(localSavePath)) FolderOperations.Delete(localSavePath);
                FolderOperations.Copy(remoteRoot, localSavePath);
                //弹出提示框
                StackPanel stackPanel = new();
                stackPanel.Children.Add(new TextBlock {Text = "GalgameCollectionService_CreateSymbolicLinkFailed".GetLocalized()});
                stackPanel.Children.Add(new TextBlock
                {
                    Text = e.Message + "\n" + e.StackTrace,
                    TextWrapping = TextWrapping.Wrap
                });
                ContentDialog dialog = new()
                {
                    XamlRoot = App.MainWindow!.Content?.XamlRoot,
                    RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
                    Title = "Error".GetLocalized(),
                    Content = stackPanel,
                    PrimaryButtonText = "Yes".GetLocalized()
                };
                await dialog.ShowAsync();
            }
        }

        if (installation.Source is not null) _galSrcService.Save(installation.Source);
        await SaveGalgameAsync(galgame);
    }

    /// <summary>
    /// 从设置中读取bangumi的设置
    /// </summary>
    private async Task<BgmPhraserData> GetBgmData()
    {
        BgmPhraserData data = new()
        {
            Token = (await LocalSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount))?.BangumiAccessToken ?? ""
        };
        return data;
    }

    /// <summary>
    /// 从设置中读取Vndb的设置
    /// </summary>
    private async Task<VndbPhraserData> GetVndbData()
    {
        LanguageEnum language = LocalSettingsService.ReadSettingAsync<LanguageEnum>(KeyValues.Language).Result;
        var isChineseCulture = language == LanguageEnum.ChineseSimplified ||
                                (language == LanguageEnum.Auto &&
                                 System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh"));

        var translateTags = await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.VndbTranslateTags);
        var censorTags = await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.VndbCensorTags);
        var removeSpoilerTags = await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.VndbRemoveSpoilerTags);

        VndbPhraserData data = new()
        {
            Token = (await LocalSettingsService.ReadSettingAsync<VndbAccount>(KeyValues.VndbAccount))?.Token,
            IsChineseCulture = isChineseCulture,
            TranslateTags = translateTags,
            CensorTags = censorTags,
            RemoveSpoilerTags = removeSpoilerTags
        };
        return data;
    }

    private MixedPhraserData GetMixData()
    {
        return new MixedPhraserData
        {
            Order = LocalSettingsService.ReadSettingAsync<MixedPhraserOrder>(KeyValues.MixedPhraserOrder).Result!,
            Enabled = LocalSettingsService.ReadSettingAsync<MixedPhraserEnabled>(KeyValues.MixedPhraserEnabled).Result ?? new MixedPhraserEnabled(),
            TimeoutSeconds = LocalSettingsService.ReadSettingAsync<int>(KeyValues.MixedPhraserTimeout).Result,
        };
    }

    private async Task OnSettingChanged(string key)
    {
        switch (key)
        {
            case KeyValues.BangumiAccount:
                PhraserList[(int)RssType.Bangumi].UpdateData(await GetBgmData());
                break;
            case KeyValues.VndbAccount:
                PhraserList[(int)RssType.Vndb].UpdateData(await GetVndbData());
                break;
            case KeyValues.VndbTranslateTags:
            case KeyValues.VndbCensorTags:
            case KeyValues.VndbRemoveSpoilerTags:
                PhraserList[(int)RssType.Vndb].UpdateData(await GetVndbData());
                break;
            case KeyValues.MixedPhraserOrder:
            case KeyValues.MixedPhraserEnabled:
            case KeyValues.MixedPhraserTimeout:
                PhraserList[(int)RssType.Mixed].UpdateData(GetMixData());
                break;
        }
    }

    private async void OnPluginLoaded(object recipient, PluginLoadArgs message)
    {
        try
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (message.Plugin is IParserProvider provider)
            {
                IGalInfoPhraser parser = provider.GetPhraser();
                RssType type = parser.GetPhraseType();
                PhraserList[(int)type] = parser;
                EnumExtension.Register(type.GetType(), (int)type, provider.ParserName);
            }

            // 插件加载后（可能包含网络代理插件注册了 DefaultProxy），刷新内置 Bangumi 数据源以应用最新代理
            PhraserList[(int)RssType.Bangumi].UpdateData(await GetBgmData());
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    #region UPGRADE
    private async Task MixedPhraserOrderUpdate()
    {
        try
        {
            MixedPhraserOrder orders =
                (await LocalSettingsService.ReadSettingAsync<MixedPhraserOrder>(KeyValues.MixedPhraserOrder))!;
            IEnumerable<PropertyInfo> properties = orders.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(ObservableCollection<RssType>));

            LanguageEnum language = LocalSettingsService.ReadSettingAsync<LanguageEnum>(KeyValues.Language).Result;
            var isChineseCulture = language == LanguageEnum.ChineseSimplified ||
                                    (language == LanguageEnum.Auto &&
                                        System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh"));

            MixedPhraserOrder defOrder = new MixedPhraserOrder().SetToDefault(isChineseCulture);
            foreach (PropertyInfo prop in properties)
            {
                ObservableCollection<RssType> order = (ObservableCollection<RssType>)prop.GetValue(orders)!;
                ObservableCollection<RssType> target = (ObservableCollection<RssType>)prop.GetValue(defOrder)!;
                foreach (RssType type in target.Where(type => !order.Contains(type)))
                {
                    // 尽量插入到默认顺序中紧随其后的已有元素之前，使新增搜刮器的相对位置与默认配置一致（如Hikarinagi位于Bangumi前）
                    int insertIndex = -1;
                    for (int i = target.IndexOf(type) + 1; i < target.Count && insertIndex < 0; i++)
                        insertIndex = order.IndexOf(target[i]);
                    if (insertIndex >= 0) order.Insert(insertIndex, type);
                    else order.Add(type);
                }
            }

            await LocalSettingsService.SaveSettingAsync(KeyValues.MixedPhraserOrderVersion,
                MixedPhraserOrder.Version);
            await LocalSettingsService.SaveSettingAsync(KeyValues.MixedPhraserOrder, orders);
        }
        catch (Exception e) //不应该发生
        {
            _infoService.Event(EventType.AppError, InfoBarSeverity.Error, "Upgrade failed", e);
        }
    }

    /// <summary>
    /// 升级存储格式到LiteDB
    /// </summary>
    /// <returns></returns>
    private async Task UpgradeToLiteDb()
    {
        LocalSettingStatus status = await LocalSettingsService.ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true) ?? new();
        if (status.GameLiteDbUpgrade) return;
        try
        {
            foreach (Galgame game in _galgames)
                _dbSet.Upsert(game);
            await LocalSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
            await LocalSettingsService.RemoveSettingAsync(KeyValues.Galgames, true); //先保存标识再删除，防止删除出错导致读取继续使用旧json方案
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.UpgradeError, InfoBarSeverity.Warning, "GalgameCollectionService_UpgradeToLiteDB_Failed".GetLocalized(), e);
        }
        status.GameLiteDbUpgrade = true;
    }

    #endregion

    private async Task ImportAsync()
    {
        LocalSettingStatus? status =
            await LocalSettingsService.ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true);
        if (status?.ImportGalgame is not false) return;
        foreach (Galgame game in await LocalSettingsService.ReadSettingAsync<List<Galgame>>
                     (KeyValues.Galgames, true) ?? [])
            _galgames.Add(game);
        foreach (Galgame game in _galgames)
        {
            game.ImagePath.ForceSet(await LocalSettingsService.GetImageFromImportAsync(game.ImagePath.Value));
            game.HeaderImagePath.ForceSet(await LocalSettingsService.GetImageFromImportAsync(game.HeaderImagePath.Value));
            foreach (GalgameCharacter character in game.Characters)
            {
                character.ImagePath = (await LocalSettingsService.GetImageFromImportAsync(character.ImagePath))!;
                character.PreviewImagePath =
                    (await LocalSettingsService.GetImageFromImportAsync(character.PreviewImagePath))!;
            }
        }
        status.ImportGalgame = true;
        await LocalSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
        await SaveGalgamesAsync();
        _galgames.Clear(); //只是为了保存临时借用数组，还原回之前的状态（全空）
    }
}

public class FolderOrFilePickerDialog : ContentDialog
{
    private readonly bool _isFirstItemSuggested;
    private string? _selectedItem;
    private readonly TaskCompletionSource<string?> _folderSelectedTcs = new();

    public FolderOrFilePickerDialog(
        XamlRoot xamlRoot,
        string title,
        List<string> entries,
        bool isFolder = true,
        string? suggestedPath = null,
        bool isFirstItemSuggested = false)
    {
        _isFirstItemSuggested = isFirstItemSuggested;

        XamlRoot = xamlRoot;
        Title = title;
        Content = CreateContent(entries);

        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "GalgameCollectionService_FolderOrFilePickerDialog_Other".GetLocalized();
        CloseButtonText = "Cancel".GetLocalized();

        IsPrimaryButtonEnabled = false;

        PrimaryButtonClick += (_, _) => { _folderSelectedTcs.TrySetResult(_selectedItem); };
        SecondaryButtonClick += (_, _) =>
        {
            if (isFolder)
            {
                PvnFolderPicker folderPicker = new()
                {
                    InitialDirectory = suggestedPath,
                };
                PickerResult result = folderPicker.ShowDialog();
                if (result == PickerResult.OK)
                {
                    _selectedItem = folderPicker.SelectedPath;
                    _folderSelectedTcs.TrySetResult(_selectedItem);
                }
                else
                {
                    _folderSelectedTcs.TrySetResult(null);
                }
            }
            else
            {
                PvnFilePicker filePicker = new()
                {
                    AllowMultiSelect = false,
                    InitialDirectory = suggestedPath,
                };
                PickerResult result = filePicker.ShowDialog();
                if (result == PickerResult.OK)
                {
                    _selectedItem = filePicker.SelectedPath;
                    _folderSelectedTcs.TrySetResult(_selectedItem);
                }
                else
                {
                    _folderSelectedTcs.TrySetResult(null);
                }
            }
        };
        CloseButtonClick += (_, _) => { _folderSelectedTcs.TrySetResult(null); };
    }

    private UIElement CreateContent(List<string> entries)
    {
        StackPanel stackPanel = new();
        foreach (var entry in entries)
        {
            RadioButton radioButton = new()
            {
                Content = entry,
                GroupName = "ExeFiles",
            };
            radioButton.Checked += RadioButton_Checked;
            stackPanel.Children.Add(radioButton);
        }

        if (_isFirstItemSuggested && stackPanel.Children.OfType<RadioButton>().FirstOrDefault() is { } firstElement)
        {
            firstElement.IsChecked = true;
        }

        return stackPanel;
    }

    private void RadioButton_Checked(object sender, RoutedEventArgs? e)
    {
        RadioButton radioButton = (RadioButton)sender;
        _selectedItem = radioButton.Content.ToString()!;
        IsPrimaryButtonEnabled = true;
        DefaultButton = ContentDialogButton.Primary;
    }

    public async Task<string?> ShowAndAwaitResultAsync()
    {
        await ShowAsync();
        return await _folderSelectedTcs.Task;
    }
}
