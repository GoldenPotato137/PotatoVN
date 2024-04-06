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

public class GalgameSourceCollectionService : IDataCollectionService<GalgameSourceBase>
{
    private ObservableCollection<GalgameSourceBase> _galgameSources = new();
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IBgTaskService _bgTaskService;

    public GalgameSourceCollectionService(ILocalSettingsService localSettingsService, 
        IDataCollectionService<Galgame> galgameService, IBgTaskService bgTaskService)
    {
        _localSettingsService = localSettingsService;
        _galgameService = ((GalgameCollectionService?)galgameService)!;
        _galgameService.GalgameAddedEvent += OnGalgameAdded;
        _galgameService.GalgameDeletedEvent += OnGalgameDeleted;
        _bgTaskService = bgTaskService;
    }

    public async Task<ObservableCollection<GalgameSourceBase>> GetGalgameSourcesAsync()
    {
        await Task.CompletedTask;
        return _galgameSources;
    }

    public async Task InitAsync()
    {
        _galgameSources = await _localSettingsService.ReadSettingAsync<ObservableCollection<GalgameSourceBase>>(KeyValues.GalgameSources, true)
                          ?? new ObservableCollection<GalgameSourceBase>();
        List<Galgame> galgames = _galgameService.Galgames;

        await Task.Run(() =>
        {
            foreach (GalgameSourceBase galgameFolder in _galgameSources)
            {
                galgameFolder.GalgameService = _galgameService;
                foreach(Galgame game in galgames.Where(galgame => galgameFolder.IsInSource(galgame)))
                    galgameFolder.AddGalgame(game);
            }
        });
    }

    public Task StartAsync()
    {
        foreach (GalgameSourceBase source in _galgameSources.Where(f => f.ScanOnStart))
        {
            _bgTaskService.AddBgTask(new GetGalgameInSourceTask(source));
        }
        return Task.CompletedTask;
    }

    private void OnGalgameAdded(Galgame galgame)
    {
        if (_galgameSources.FirstOrDefault(folder => folder.IsInSource(galgame)) is { } sourceBase)
        {
            sourceBase.AddGalgame(galgame);
        }
    }

    private void OnGalgameDeleted(Galgame galgame)
    {
        _galgameSources.FirstOrDefault(folder => folder.IsInSource(galgame))?.DeleteGalgame(galgame);
    }

    /// <summary>
    /// 试图添加一个galgame库
    /// </summary>
    /// <param name="sourceType"></param>
    /// <param name="path">库路径</param>
    /// <param name="tryGetGalgame">是否自动寻找库里游戏</param>
    /// <exception cref="Exception">库已经添加过了</exception>
    public async Task AddGalgameSourceAsync(GalgameSourceType sourceType, string path, bool tryGetGalgame = true)
    {
        if (_galgameSources.Any(galFolder => galFolder.Path == path && galFolder.SourceType == sourceType))
        {
            throw new Exception($"这个galgame库{sourceType.SourceTypeToString()}://{path}已经添加过了");
        }

        GalgameSourceBase? galgameSource = null;

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
        if (galgameSource is null) throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null);
        _galgameSources.Add(galgameSource);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameSources, _galgameSources, true);
        if (tryGetGalgame)
        {
            await _bgTaskService.AddBgTask(new GetGalgameInSourceTask(galgameSource));
        }
    }

    /// <summary>
    /// 删除一个galgame库，其包含弹窗警告，若用户取消则什么都不做
    /// </summary>
    /// <param name="galgameFolderSource"></param>
    public async Task DeleteGalgameFolderAsync(GalgameSourceBase galgameFolderSource)
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
        
        if (delete == false || !_galgameSources.Contains(galgameFolderSource)) return;
        foreach (Galgame galgame in await galgameFolderSource.GetGalgameList())
            await _galgameService.RemoveGalgame(galgame, true);
        _galgameSources.Remove(galgameFolderSource);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameSources, _galgameSources, true);
    }
    
    /// <summary>
    /// 找到一个galgame库，若不存在则返回null
    /// </summary>
    public GalgameSourceBase? GetGalgameSourceFromUrl(string url)
    {
        return _galgameSources.FirstOrDefault(s => s.Url == url);
    }

    /// <summary>
    /// 扫描所有库
    /// </summary>
    public void ScanAll()
    {
        foreach(GalgameSourceBase b in _galgameSources)
            _bgTaskService.AddBgTask(new GetGalgameInSourceTask(b));
    }
}

