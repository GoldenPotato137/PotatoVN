using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class GalgameSourceCollectionService : IGalgameSourceCollectionService
{
    private ObservableCollection<GalgameSourceBase> _galgameSources = new();
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IInfoService _infoService;

    public GalgameSourceCollectionService(ILocalSettingsService localSettingsService, 
        IGalgameCollectionService galgameService, IBgTaskService bgTaskService,
        IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _galgameService = ((GalgameCollectionService?)galgameService)!;
        _galgameService.GalgameDeletedEvent += OnGalgameDeleted;
        _bgTaskService = bgTaskService;
        _infoService = infoService;
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

        foreach (Galgame galgame in source.GetGalgameList())
        {
            source.DeleteGalgame(galgame); //这里不要用RemoveFromSource，因为这里只是取消托管，而不是真的从库中物理移除游戏
            if(galgame.Sources.Count == 0)
                await _galgameService.RemoveGalgame(galgame, true);
        }
        _galgameSources.Remove(source);
        await Save();
    }

    public BgTaskBase MoveIntoSourceAsync(GalgameSourceBase target, Galgame game, bool operate, string? path = null)
    {
        if (game.Sources.Any(s => s == target))
        {
            _infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Warning,
                "UnexpectedEvent".GetLocalized(),
                new PvnException($"Can not move game {{game.Name}} into source {{target.Path}}: already there"));
            return BgTaskBase.Empty;
        }
        if (!operate)
        {
            if (path is null) throw new ArgumentException("operate is false but path is null");
            target.AddGalgame(game, path);
            return BgTaskBase.Empty;
        }
        return SourceServiceFactory.GetSourceService(target.SourceType).MoveInAsync(target, game, path);
    }

    public BgTaskBase RemoveFromSourceAsync(GalgameSourceBase target, Galgame game, bool operate)
    {
        if (game.Sources.All(s => s != target))
        {
            _infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Warning,
                "UnexpectedEvent".GetLocalized(), new PvnException($"Can not move game {game.Name} " +
                                                                   $"out of source {target.Path}: not in source"));
            return BgTaskBase.Empty;
        }
        if (!operate)
        {
            target.DeleteGalgame(game);
            return BgTaskBase.Empty;
        }
        return SourceServiceFactory.GetSourceService(target.SourceType).MoveOutAsync(target, game);
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

    private void OnGalgameDeleted(Galgame galgame)
    {
        foreach (GalgameSourceBase s in galgame.Sources)
            RemoveFromSourceAsync(s, galgame, false);
    }

    /// <summary>
    /// 将galgame源归属记录从galgame移入source管理
    /// </summary>
    private async Task SourceUpgradeAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SourceUpgrade)) return;
        // 将游戏搬入对应的源中
        List<Galgame> games = _galgameService.Galgames;
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
                await MoveIntoSourceAsync(source, g, false, folderPath).Task;
            }
            else //非本机游戏
            {
                GalgameSourceBase source = GetGalgameSource(GalgameSourceType.Virtual, string.Empty)!;
                await MoveIntoSourceAsync(source, g, false, string.Empty).Task;
            }
        }

        await Save();
        await _localSettingsService.SaveSettingAsync(KeyValues.SourceUpgrade, true);
    }
}

