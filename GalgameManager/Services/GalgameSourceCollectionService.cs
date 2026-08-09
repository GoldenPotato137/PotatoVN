using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services; 
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Views.Dialog;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class GalgameSourceCollectionService(
    ILocalSettingsService localSettingsService,
    IBgTaskService bgTaskService,
    IInfoService infoService,
    IServiceProvider serviceProvider)
    : IGalgameSourceCollectionService
{
    public Action<GalgameSourceBase>? OnSourceDeleted { get; set; }
    public Action? OnSourceChanged { get; set; }

    private ObservableCollection<GalgameSourceBase> _galgameSources = new();

    private readonly List<JsonConverter> _converters =
    [
        new GalgameAndUidConverter(),
        new GalgameSourceConverter(),
    ];
    private ILiteCollection<GalgameSourceBase> _dbSet = null!;

    private IGalgameCollectionService? _gameService;
    /// 懒解析以打破与GalgameCollectionService之间的构造循环依赖
    private IGalgameCollectionService GameService =>
        _gameService ??= serviceProvider.GetRequiredService<IGalgameCollectionService>();

    public async Task InitAsync()
    {
        _dbSet = localSettingsService.Database.GetCollection<GalgameSourceBase>("source");
        LocalSettingStatus settingStatus = await localSettingsService.ReadSettingAsync<LocalSettingStatus>
            (KeyValues.DataStatus, true) ?? new();
        await LiteDbUpgrade(settingStatus);
        LoadData();
        await SourceUpgradeAsync(settingStatus);
        await VirtualSourceUpgrade(settingStatus);
        foreach (GalgameSourceBase source in _galgameSources) // 部分崩溃的情况可能导致source里面部分galgame为null
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            List<GalgameAndPath> tmp = source.Galgames.Where(g => g.Galgame is null).ToList();
            foreach (GalgameAndPath g in tmp)
            {
                source.Galgames.Remove(g);
                infoService.DeveloperEvent(InfoBarSeverity.Error,
                    "GalgameSourceCollectionService_InitAsync_GalgameIsNull".GetLocalized(g.Path, source.Url));
            }
        }
        foreach (Galgame game in GameService.Galgames)
            game.EnsurePreferredInstallation();
        // 去除找不到的库（只对启用了启动检查的库进行检查）
        // 升级 E2E 使用历史路径夹具，不应按当前机器文件系统删除这些来源。
        if (!AppStoragePaths.IsUpgradeUiTest)
        {
            List<GalgameSourceBase> toRemove = _galgameSources.Where(source =>
                source is { CheckOnStart: true, SourceType: GalgameSourceType.LocalFolder } && !Directory.Exists(source.Path)).ToList();
            if (toRemove.Count > 0)
            {
                foreach (GalgameSourceBase source in toRemove)
                {
                    foreach (GalgameAndPath entry in source.Galgames.ToList())
                        entry.Galgame.DetachSourceEntry(entry);
                    _galgameSources.Remove(source);
                    _dbSet.Delete(source.Id);
                }

                infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                    "GalgameSourceCollectionService_RemoveNonExist_Title".GetLocalized(),
                    msg: "GalgameSourceCollectionService_RemoveNonExist_Msg".GetLocalized(
                        $"\n{string.Join('\n', toRemove.Select(s => s.Path))}"));
            }
        }
        await ImportAsync(settingStatus);
        await MetaBackupSettingsUpgrade(settingStatus);
        await RemoveableUpgrade(settingStatus);
        await MultiInstallUpgrade(settingStatus);
        // 计算子库
        CalcSubSources();
        // 添加监听变动检测
        foreach (GalgameSourceBase s in _galgameSources)
        {
            s.DetectChanged += DetectionChanged;
            DetectionChanged(s); // 手动触发一次，挂上监听（如果这个库之前有设置监听需求）
        }
        return;

        void LoadData()
        {
            _galgameSources.Clear();
            _galgameSources.SyncCollection(_dbSet.FindAll().ToList());
            IGalgameCollectionService gameService = GameService;
            foreach (GalgameSourceBase source in _galgameSources)
            {
                foreach (GalgameAndPathDbDto dto in source.GetLoadedGalgames())
                {
                    if (gameService.GetGalgameFromUuid(dto.GalgameId) is { } game)
                    {
                        GalgameAndPath entry = new(game, dto.Path, source,
                            dto.EntryId == Guid.Empty ? null : dto.EntryId, dto.LocalConfig);
                        source.Galgames.Add(entry);
                        game.AttachSourceEntry(entry);
                    }
                    else
                    {
                        infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Warning,
                            title: "GalgameSourceCollectionService_NotSuchGameUUID_Title".GetLocalized(),
                            msg: "GalgameSourceCollectionService_NotSuchGameUUID_Msg".GetLocalized(dto.Path,
                                source.Name));
                        Save(source);
                    }
                }
            }
        }
    }

    public async Task StartAsync()
    {
        // 检查所有库中的游戏是否还在源中
        List<(Task<List<Galgame>>, GalgameSourceBase)> sourceCheckTasks = new();
        foreach (GalgameSourceBase source in _galgameSources)
            sourceCheckTasks.Add((CheckGamesInSourceAsync(source), source));
        foreach ((Task<List<Galgame>> task, GalgameSourceBase source) t in sourceCheckTasks)
        {
            try
            {
                List<Galgame> removedGames = await t.task;
                if (removedGames.Count > 0)
                    infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                        "GalgameSourceCollectionService_CheckGamesInSource_Title".GetLocalized(t.source.Name),
                        msg: "GalgameSourceCollectionService_CheckGamesInSource_Msg".GetLocalized(
                            $"\n{string.Join('\n', removedGames.Select(g => g.Name.Value))}"));
            }
            catch (Exception e)
            {
                infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error,
                    title: "GalgameSourceCollectionService_CheckGamesInSourceFailed".GetLocalized(t.source.Name),
                    exception: e);
            }
        }
        
        foreach (GalgameSourceBase source in _galgameSources.Where(f => f.ScanOnStart)) 
            _ = bgTaskService.AddBgTask(new GetGalgameInSourceTask(source));
    }
    
    public ObservableCollection<GalgameSourceBase> GetGalgameSources() => _galgameSources;
    
    public GalgameSourceBase? GetGalgameSourceFromUrl(string url)
    {
        try
        {
            (GalgameSourceType type, var path) = GalgameSourceBase.ResolveUrl(url);
            return GetGalgameSource(type, path);
        }
        catch (Exception e)
        {
            infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error, e.Message, e);
            return null;
        }
        
    }

    public GalgameSourceBase? GetGalgameSourceFromId(Guid id) => _galgameSources.FirstOrDefault(s => s.Id == id);

    public GalgameSourceBase? GetGalgameSource(GalgameSourceType type, string path)
    {
        IEnumerable<GalgameSourceBase> tmp = _galgameSources.Where(s => s.SourceType == type);
        switch (type)
        {
            case GalgameSourceType.LocalFolder:
            case GalgameSourceType.Steam:
                return tmp.FirstOrDefault(s => Utils.ArePathsEqual(s.Path, path));
            case GalgameSourceType.Virtual:
                return tmp.FirstOrDefault(s => s.SourceType == GalgameSourceType.Virtual);
            case GalgameSourceType.UnKnown:
            case GalgameSourceType.LocalZip:
            default:
                return tmp.FirstOrDefault(s => s.Path == path);
        }
    }

    public async Task<GalgameSourceBase> AddGalgameSourceAsync(GalgameSourceType sourceType, string path,
        bool tryGetGalgame = true, bool manualSelectFolder = false)
    {
        if (_galgameSources.Any(galFolder => galFolder.Path == path && galFolder.SourceType == sourceType))
        {
            throw new PvnException($"这个galgame库{sourceType.SourceTypeToString()}://{path}已经添加过了");
        }

        GalgameSourceBase? galgameSource;

        switch (sourceType)
        {
            case GalgameSourceType.UnKnown:
                throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null);
            case GalgameSourceType.LocalFolder:
                galgameSource = new GalgameFolderSource(path).UpdateRemoveable();
                break;
            case GalgameSourceType.LocalZip:
                galgameSource = new GalgameZipSource(path);
                break;
            case GalgameSourceType.Steam:
                galgameSource = new SteamSource(path);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null);
        }
        _galgameSources.Add(galgameSource);
        Save(galgameSource);
        if (manualSelectFolder) await new SelectToScanFolderDialog(galgameSource).ShowAsync();
        if (tryGetGalgame)
        {
            await bgTaskService.AddBgTask(new GetGalgameInSourceTask(galgameSource));
        }
        
        CalcSubSources();
        galgameSource.DetectChanged += DetectionChanged;
        DetectionChanged(galgameSource); // 手动触发一次，挂上监听
        UiThreadInvokeHelper.Invoke(() => OnSourceChanged?.Invoke());
        
        return galgameSource;
    }
    
    public async Task DeleteGalgameFolderAsync(GalgameSourceBase source)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "GalgameFolderCollectionService_DeleteGalgameFolderAsync_Title".GetLocalized(),
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "GalgameFolderCollectionService_DeleteGalgameFolderAsync_Content".GetLocalized(), TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
                    new CheckBox { Content = "GalgameFolderCollectionService_DeleteGalgameFolderAsync_RemoveFromLibrary".GetLocalized(), Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0), IsChecked = false },
                }
            },
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Secondary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        CheckBox checkBox = (CheckBox)((StackPanel)dialog.Content).Children[1];
        await DeleteGalgameFolderAsync(source, checkBox.IsChecked ?? false);
    }

    public async Task DeleteGalgameFolderAsync(GalgameSourceBase source, bool removeGames)
    {
        if (!_galgameSources.Contains(source)) return;

        List<GalgameSourceBase> sourcesToDelete = [source];
        CollectAllSubSources(source, sourcesToDelete);

        foreach (GalgameSourceBase sourceToDelete in sourcesToDelete)
        {
            try
            {
                foreach (GalgameAndPath entry in sourceToDelete.Galgames.ToList())
                {
                    Galgame game = entry.Galgame;
                    await MoveOutNoOperate(entry);
                    if (removeGames && game.Sources.Count == 0)
                        await GameService.RemoveGalgame(game, false);
                }
            }
            catch (Exception e)
            {
                infoService.DeveloperEvent(InfoBarSeverity.Error,
                    msg: $"Failed to move game out of source {sourceToDelete.Url}\n{e.StackTrace}");
            }
        }

        foreach (GalgameSourceBase sourceToDelete in sourcesToDelete)
        {
            _galgameSources.Remove(sourceToDelete);
            _dbSet.Delete(sourceToDelete.Id);
            sourceToDelete.Detect = false;
            OnSourceDeleted?.Invoke(sourceToDelete);
        }

        CalcSubSources();
        OnSourceChanged?.Invoke();
    }
    
    // 递归收集所有子源
    private void CollectAllSubSources(GalgameSourceBase source, List<GalgameSourceBase> sourcesToDelete)
    {
        foreach (var subSource in source.SubSources.ToList())
        {
            if (!sourcesToDelete.Contains(subSource))
            {
                sourcesToDelete.Add(subSource);
                CollectAllSubSources(subSource, sourcesToDelete);
            }
        }
    }

    /// <inheritdoc />
    public GalgameAndPath? MoveInNoOperate(GalgameSourceBase target, Galgame game, string path,
        LocalInstallationConfig? localConfig = null)
    {
        if (game.Sources.Any(s => s == target))
        {
            infoService.DeveloperEvent(
                e: new PvnException($"Can not move game {game.Name.Value} into source {target.Path}: already there"));
            return null;
        }
        GalgameAndPath entry = target.AddGalgame(game, path, localConfig: localConfig);
        Save(target);
        return entry;
    }

    /// <inheritdoc />
    public async Task MoveOutNoOperate(GalgameAndPath installation, bool deleteFiles = false)
    {
        GalgameSourceBase? source = installation.Source;
        if (source is null || !source.Galgames.Contains(installation))
        {
            infoService.DeveloperEvent(e: new PvnException(
                $"Can not remove source entry {installation.EntryId}: source entry not attached"));
            return;
        }
        if (deleteFiles) await DeleteInstallationFilesAsync(installation);
        source.DeleteGalgame(installation.Galgame);
        Save(source);
        await GameService.SaveGalgameAsync(installation.Galgame);
    }

    private static Task DeleteInstallationFilesAsync(GalgameAndPath installation)
    {
        if (installation.Source is not GalgameFolderSource)
            throw new PvnException("MultiInstall_DeleteFiles_LocalFolderOnly".GetLocalized());
        return Task.Run(() =>
        {
            if (Directory.Exists(installation.Path))
                new DirectoryInfo(installation.Path).Delete(true);
        });
    }

    public BgTaskBase MoveAsync(GalgameSourceBase? moveInSrc, string? moveInPath, GalgameSourceBase? moveOutSrc, Galgame game)
    {
        if (game.Sources.Any(s => s == moveInSrc))
        {
            infoService.DeveloperEvent(e: new PvnException($"{game.Name.Value} is already in {moveInSrc!.Url}"));
            moveInSrc = null;
            moveInPath = null;
        }
        if (moveOutSrc is not null && game.Sources.All(s => s != moveOutSrc))
        {
            infoService.DeveloperEvent(e: new PvnException($"{game.Name.Value} is not in {moveOutSrc.Url}"));
            moveOutSrc = null;
        }
        SourceMoveTask task = new(game, moveInSrc, moveInPath, moveOutSrc);
        bgTaskService.AddBgTask(task);
        return task;
    }

    public string GetSourcePath(GalgameSourceType type, string gamePath)
    {
        return SourceServiceFactory.GetSourceService(type).GetSourcePathAsync(gamePath).Result;
    }

    public async Task ExportAsync(Action<string, int, int>? progress)
    {
        ObservableCollection<GalgameSourceBase> exportData = new();
        for (var i = 0; i < _galgameSources.Count; i++)
        {
            GalgameSourceBase source = _galgameSources[i];
            progress?.Invoke("GalgameSourceCollectionService_Export_Progress".GetLocalized(source.Name), i + 1,
                _galgameSources.Count);
            GalgameSourceBase clone = source.DeepClone(new JsonSerializerSettings { Converters = _converters });
            clone.ImagePath = await localSettingsService.AddImageToExportAsync(clone.ImagePath);
            exportData.Add(clone);
        }

        await localSettingsService.AddToExportAsync(KeyValues.GalgameSources, exportData, converters: _converters);
    }

    /// <summary>
    /// 扫描所有库
    /// </summary>
    public void ScanAll()
    {
        foreach(GalgameSourceBase b in _galgameSources)
            bgTaskService.AddBgTask(new GetGalgameInSourceTask(b));

    }

    /// <summary>   
    /// 扫描某个库
    /// </summary>
    /// <param name="source"></param>
    public void Scan(GalgameSourceBase source)
    {
        bgTaskService.AddBgTask(new GetGalgameInSourceTask(source));
    }
    
    /// <summary>
    /// 保存所有游戏库
    /// </summary>
    private async Task SaveAllAsync() => await Task.Run(() => { _dbSet.Upsert(_galgameSources); });

    public void Save(GalgameSourceBase source) => _dbSet.Upsert(source);

    /// <summary>
    /// 重新计算所有库的归属关系
    /// </summary>
    private void CalcSubSources()
    {
        // 确实有O(nlogn)的写法，但不是特别有必要，先O(n^2)吧
        foreach (GalgameSourceBase src in _galgameSources)
        {
            src.ParentSource = null;
            src.SubSources.Clear();
        }

        foreach (GalgameSourceBase src in _galgameSources)
        {
            GalgameSourceBase? target = null;
            foreach (GalgameSourceBase current in _galgameSources)
                if (src != current && Utils.IsPathContained(current.Path, src.Path) &&
                    (target is null || current.Path.Length > target.Path.Length) &&
                    !Utils.ArePathsEqual(src.Path, current.Path))
                    target = current;
            src.ParentSource = target;
            target?.SubSources.Add(src);
        }
    }

    /// 检查某个源的游戏是否还在源中，如果不在则移出
    private Task<List<Galgame>> CheckGamesInSourceAsync(GalgameSourceBase source)
    {
        if (source is ILocalGalgameSource)
        {
            return Task.Run(async () =>
            {
                List<GalgameAndPath> entriesToRemove =
                    source.Galgames.Where(entry => !Directory.Exists(entry.Path)).ToList();
                foreach (GalgameAndPath entry in entriesToRemove)
                    await MoveOutNoOperate(entry);
                return entriesToRemove.Select(entry => entry.Galgame).ToList();
            });
        }

        switch (source.SourceType)
        {
            case GalgameSourceType.Virtual: 
                return Task.FromResult(new List<Galgame>());
            case GalgameSourceType.LocalFolder:
            case GalgameSourceType.Steam:
            case GalgameSourceType.LocalZip:
            case GalgameSourceType.UnKnown:
            default:
                throw new NotSupportedException();
        }
    }

    private async Task ImportAsync(LocalSettingStatus status)
    {
        if (status.ImportGalgameSource) return;
        foreach (GalgameSourceBase source in _galgameSources)
        {
            source.ImagePath = await localSettingsService.GetImageFromImportAsync(source.ImagePath);
        }
        status.ImportGalgameSource = true;
        await SaveAllAsync();
        await localSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
    }

    #region DETECTION SOURCE CHANGE

    private void DetectionChanged(GalgameSourceBase source)
    {
        Task.Run(async () =>
        {
            try
            {
                IGalgameSourceService srcHandler = SourceServiceFactory.GetSourceService(source.SourceType);
                await srcHandler.RemoveListenAsync(source); // 先移除旧有监听
                if (source.Detect) await srcHandler.AddListenAsync(source);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        });
    }

    #endregion
    
    #region UPGRADES
    
    /// <summary>
    /// <b>since v.1.8.0</b><br/>
    /// 1. 修改存储库的结构（data.galgameFolders.json -> data.galgameSources.json, GalgameFolder -> GalgameSourceBase）<br/>
    /// 2. 给各库命名<br/>
    /// 3. 将galgame源归属记录从galgame移入source管理 <br/>
    /// </summary>
    private async Task SourceUpgradeAsync(LocalSettingStatus status)
    {
        if (status.GalgameSourceFormatUpgrade) return;
        // 修改存储库结构
        try
        {
            var template = new[] // 旧的GalgameFolder存储库结构
            {
                new
                {
                    Path = string.Empty,
                    ScanOnStart = false,
                },
            };
            var tmp = await localSettingsService.ReadOldSettingAsync(KeyValues.GalgameFolders, template);
            if (tmp is not null)
            {
                foreach (var folder in tmp.Where(f => !string.IsNullOrEmpty(f.Path)))
                {
                    GalgameFolderSource source = new(folder.Path) { ScanOnStart = folder.ScanOnStart };
                    _galgameSources.Add(source);
                }
            }
            await localSettingsService.RemoveSettingAsync(KeyValues.GalgameFolders, true);
        }
        catch (Exception e) //不应该发生
        {
            infoService.Event(EventType.UpgradeError, InfoBarSeverity.Warning, "升级游戏库数据库结构失败", e);
        }
        // 给各库命名
        {
            foreach (GalgameSourceBase src in _galgameSources)
                src.SetNameFromPath();
        }
        // 将游戏搬入对应的源中
        {
            IList<Galgame> games = GameService.Galgames;
            foreach (Galgame g in games)
            {
#pragma warning disable CS0618 // 类型或成员已过时，升级旧数据使用
                var gamePath = g.Path;
#pragma warning restore CS0618 // 类型或成员已过时
                if (!string.IsNullOrEmpty(gamePath))
                {
                    var folderPath = Path.GetDirectoryName(gamePath);
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error,
                            "UnexpectedEvent".GetLocalized(),
                            new PvnException($"Can not get the parent folder of the game{gamePath}"));
                        continue;
                    }

                    GalgameSourceBase? source = GetGalgameSource(GalgameSourceType.LocalFolder, folderPath);
                    source ??= await AddGalgameSourceAsync(GalgameSourceType.LocalFolder, folderPath);
                    MoveInNoOperate(source, g, gamePath);
                }
            }
        }
        
        await SaveAllAsync();
        status.GalgameSourceFormatUpgrade = true;
        await localSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
    }
    
    /// <summary>
    /// 升级为Litedb存储数据，since v1.9.0
    /// </summary>
    private async Task LiteDbUpgrade(LocalSettingStatus status)
    {
        if (status.SourceLiteDbUpgrade) return;
        try
        {
            _galgameSources = await localSettingsService.ReadSettingAsync<ObservableCollection<GalgameSourceBase>>
                (KeyValues.GalgameSources, true, converters: _converters) ?? new();
            foreach (GalgameSourceBase source in _galgameSources)
            {
                List<GalgameAndPath> toRemove = source.Galgames
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    .Where(g => g.Galgame is null).ToList();
                foreach (GalgameAndPath g in toRemove)
                    source.Galgames.Remove(g);
            }
            await SaveAllAsync();
            await localSettingsService.RemoveSettingAsync(KeyValues.GalgameSources, true);
        }
        catch (Exception e)
        {
            infoService.Event(EventType.AppError, InfoBarSeverity.Error, "Source LiteDB upgrade failed", e);
        }
        status.SourceLiteDbUpgrade = true;
        await localSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
    }

    /// 添加非本地游戏库
    private async Task VirtualSourceUpgrade(LocalSettingStatus status)
    {
        if (status.GalgameSourceAddVirtualSource) return;
        if (_galgameSources.Any(s => s.SourceType is GalgameSourceType.Virtual)) return;
        VirtualSource source = new()
        {
            Name = "VirtualSource".GetLocalized(),
        };
        _galgameSources.Add(source);
        Save(source);
        status.GalgameSourceAddVirtualSource = true;
        await localSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
    }

    /// <summary>
    /// 将全局SaveBackupMetadata设置迁移到各个库的SaveMetaBackup属性
    /// </summary>
    private async Task MetaBackupSettingsUpgrade(LocalSettingStatus status)
    {
        if (status.MetaBackupPerSourceUpgrade) return;
        try
        {
            var globalSetting = await localSettingsService.ReadSettingAsync<bool>(KeyValues.SaveBackupMetadata);
            foreach (GalgameSourceBase source in _galgameSources)
            {
                source.SaveMetaBackup = globalSetting;
                Save(source);
            }
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
        status.MetaBackupPerSourceUpgrade = true;
        await localSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
    }

    /// <summary>
    /// 检测每个库是否为可移动介质升级
    /// </summary>
    /// <param name="status"></param>
    private async Task RemoveableUpgrade(LocalSettingStatus status)
    {
        if (status.GalgameSourceRemoveableUpgrade) return;
        try
        {
            foreach (GalgameSourceBase source in _galgameSources)
            {
                if (source is not GalgameFolderSource folderSource) continue;
                folderSource.UpdateRemoveable();
            }
            status.GalgameSourceRemoveableUpgrade = true;
            await localSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
    }

    /// <summary>
    /// 将旧版游戏级启动设置迁移到原单一安装对应的库内游戏条目。
    /// 此操作可重复执行，导入旧版数据时也能安全迁移。
    /// </summary>
    private async Task MultiInstallUpgrade(LocalSettingStatus status)
    {
        if (status.GalgameMultiInstallUpgrade) return;
        try
        {
            IGalgameCollectionService gameService = GameService;
            foreach (Galgame game in gameService.Galgames)
            {
                List<GalgameAndPath> installations = game.LocalInstallations.ToList();
                if (installations.Count == 0) continue;

                GalgameAndPath target = game.PreferredLocalInstallation ?? installations[0];
                game.ApplyLegacyLocalConfiguration(target, overwrite: false);
                game.SetPreferredInstallation(target);
            }

            await SaveAllAsync();
            await gameService.SaveGalgamesAsync();
            status.GalgameMultiInstallUpgrade = true;
            await localSettingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
        }
        catch (Exception e)
        {
            infoService.Event(EventType.UpgradeError, InfoBarSeverity.Warning,
                "Failed to upgrade local game installations", e);
        }
    }
    
    #endregion
}
