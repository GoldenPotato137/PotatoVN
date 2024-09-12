using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services; 
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class GalgameSourceCollectionService : IGalgameSourceCollectionService
{
    public Action<GalgameSourceBase>? OnSourceDeleted { get; set; }
    
    private ObservableCollection<GalgameSourceBase> _galgameSources = new();
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IInfoService _infoService;

    public GalgameSourceCollectionService(ILocalSettingsService localSettingsService, IBgTaskService bgTaskService,
        IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _bgTaskService = bgTaskService;
        _infoService = infoService;
        App.OnAppClosing += async () => await Save();
    }
    
    public async Task InitAsync()
    {
        _galgameSources = await _localSettingsService.ReadSettingAsync<ObservableCollection<GalgameSourceBase>>(
                              KeyValues.GalgameSources, true,
                              converters: new() { new GalgameAndUidConverter() })
                          ?? new ObservableCollection<GalgameSourceBase>();
        await SourceUpgradeAsync();
        // 给Galgame注入Source列表
        foreach (GalgameSourceBase s in _galgameSources)
            foreach (Galgame g in s.GetGalgameList().Where(g => !g.Sources.Contains(s)))
                g.Sources.Add(s);
    }

    public Task StartAsync()
    {
        foreach (GalgameSourceBase source in _galgameSources.Where(f => f.ScanOnStart))
        {
            _bgTaskService.AddBgTask(new GetGalgameInSourceTask(source));
        }
        return Task.CompletedTask;
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
            _infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error, e.Message, e);
            return null;
        }
        
    }

    public GalgameSourceBase? GetGalgameSource(GalgameSourceType type, string path)
    {
        IEnumerable<GalgameSourceBase> tmp = _galgameSources.Where(s => s.SourceType == type);
        switch (type)
        {
            case GalgameSourceType.LocalFolder:
                var includeSubfolder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SearchChildFolder).Result;
                return tmp.FirstOrDefault(s =>
                    includeSubfolder ? Utils.IsPathContained(s.Path, path) : Utils.ArePathsEqual(s.Path, path));
            case GalgameSourceType.Virtual:
                return tmp.FirstOrDefault() ?? AddGalgameSourceAsync(GalgameSourceType.Virtual, string.Empty).Result;
            case GalgameSourceType.UnKnown:
            case GalgameSourceType.LocalZip:
            default:
                return tmp.FirstOrDefault(s => s.Path == path);
        }
    }

    public async Task<GalgameSourceBase> AddGalgameSourceAsync(GalgameSourceType sourceType, string path,
        bool tryGetGalgame = true)
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
                galgameSource = new GalgameFolderSource(path);
                break;
            case GalgameSourceType.LocalZip:
                galgameSource = new GalgameZipSource(path);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null);
        }
        _galgameSources.Add(galgameSource);
        await Save();
        if (tryGetGalgame)
        {
            await _bgTaskService.AddBgTask(new GetGalgameInSourceTask(galgameSource));
        }
        
        return galgameSource;
    }
    
    public async Task DeleteGalgameFolderAsync(GalgameSourceBase source)
    {
        var delete = false;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "GalgameFolderCollectionService_DeleteGalgameFolderAsync_Title".GetLocalized(),
            Content = "GalgameFolderCollectionService_DeleteGalgameFolderAsync_Content".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            PrimaryButtonCommand = new RelayCommand(() => delete = true),
            DefaultButton = ContentDialogButton.Secondary
        };
        await dialog.ShowAsync();
        if (!delete || !_galgameSources.Contains(source)) return;
        
        _galgameSources.Remove(source);
        await Save();
        OnSourceDeleted?.Invoke(source);
    }

    public void MoveInNoOperate(GalgameSourceBase target, Galgame game, string path)
    {
        if (game.Sources.Any(s => s == target))
        {
            _infoService.DeveloperEvent(
                e: new PvnException($"Can not move game {game.Name.Value} into source {target.Path}: already there"));
            return;
        }
        target.AddGalgame(game, path);
    }

    public void MoveOutOperate(GalgameSourceBase target, Galgame game)
    {
        if (game.Sources.All(s => s != target))
        {
            _infoService.DeveloperEvent(e: new PvnException($"Can not move game {game.Name} " +
                                                            $"out of source {target.Path}: not in source"));
            return;
        }
        target.DeleteGalgame(game);
    }

    public BgTaskBase MoveAsync(GalgameSourceBase? moveInSrc, string? moveInPath, GalgameSourceBase? moveOutSrc, Galgame game)
    {
        if (game.Sources.Any(s => s == moveInSrc))
        {
            _infoService.DeveloperEvent(e: new PvnException($"{game.Name.Value} is already in {moveInSrc!.Url}"));
            moveInSrc = null;
            moveInPath = null;
        }
        if (moveOutSrc is not null && game.Sources.All(s => s != moveOutSrc))
        {
            _infoService.DeveloperEvent(e: new PvnException($"{game.Name.Value} is not in {moveOutSrc.Url}"));
            moveOutSrc = null;
        }
        SourceMoveTask task = new(game, moveInSrc, moveInPath, moveOutSrc);
        _bgTaskService.AddBgTask(task);
        return task;
    }

    /// <summary>
    /// 扫描所有库
    /// </summary>
    public void ScanAll()
    {
        foreach(GalgameSourceBase b in _galgameSources)
            _bgTaskService.AddBgTask(new GetGalgameInSourceTask(b));
    }
    
    private async Task Save()
    {
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameSources, _galgameSources, true,
            converters: new() { new GalgameAndUidConverter() });
    }

    /// <summary>
    /// 将galgame源归属记录从galgame移入source管理
    /// </summary>
    private async Task SourceUpgradeAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SourceUpgrade)) return;
        // 将游戏搬入对应的源中
        IList<Galgame> games = App.GetService<IGalgameCollectionService>().Galgames;
        foreach (Galgame g in games)
        {
            var gamePath = g.Path;
            if (!string.IsNullOrEmpty(gamePath))
            {
                var folderPath = Path.GetDirectoryName(gamePath);
                if (folderPath is null)
                {
                    _infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error,
                        "UnexpectedEvent".GetLocalized(),
                        new PvnException($"Can not get the parent folder of the game{gamePath}"));
                    continue;
                }

                GalgameSourceBase? source = GetGalgameSource(GalgameSourceType.LocalFolder, folderPath);
                source ??= await AddGalgameSourceAsync(GalgameSourceType.LocalFolder, folderPath);
                MoveInNoOperate(source, g, folderPath);
            }
            else //非本机游戏
            {
                GalgameSourceBase source = GetGalgameSource(GalgameSourceType.Virtual, string.Empty)!;
                MoveInNoOperate(source, g, string.Empty);
            }
        }

        await Save();
        await _localSettingsService.SaveSettingAsync(KeyValues.SourceUpgrade, true);
    }
}

