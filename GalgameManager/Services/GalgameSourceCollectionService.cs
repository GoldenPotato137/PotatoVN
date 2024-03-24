using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Models;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
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

    public async Task<ObservableCollection<GalgameSourceBase>> GetContentGridDataAsync()
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
            _bgTaskService.AddBgTask(source.GetGalgameInSourceTask());
        }
        return Task.CompletedTask;
    }

    private async void OnGalgameAdded(Galgame galgame)
    {
        if (galgame.CheckExistLocal() == false) return;
        try
        {
            await AddGalgameFolderAsync(SourceType.LocalFolder, galgame.Path[..galgame.Path.LastIndexOf('\\')], false);
        }
        catch (Exception)
        {
            // ignored
        }
        _galgameSources.First(folder => folder.IsInSource(galgame)).AddGalgame(galgame);
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
    public async Task AddGalgameFolderAsync(SourceType sourceType, string path, bool tryGetGalgame = true)
    {
        if (_galgameSources.Any(galFolder => galFolder.Path == path && galFolder.GalgameSourceType == sourceType))
        {
            throw new Exception($"这个galgame库{sourceType.SourceTypeToString()}://{path}已经添加过了");
        }

        GalgameSourceBase? galgameSource = null;

        switch (sourceType)
        {
            case SourceType.UnKnown:
                throw new NotImplementedException();
            case SourceType.LocalFolder:
                galgameSource = new GalgameFolderSource(path, _galgameService);
                break;
            case SourceType.LocalZip:
                galgameSource = new GalgameZipSource(path, _galgameService);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, null);
        }
        if (galgameSource is null) throw new NotImplementedException();
        _galgameSources.Add(galgameSource);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameSources, true);
        if (tryGetGalgame)
        {
            await _bgTaskService.AddBgTask(galgameSource.GetGalgameInSourceTask());
        }
    }

    /// <summary>
    /// 删除一个galgame库，其包含弹窗警告，若用户取消则什么都不做
    /// </summary>
    /// <param name="galgameFolderSource"></param>
    public async Task DeleteGalgameFolderAsync(GalgameFolderSource galgameFolderSource)
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
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameSources, true);
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
            _bgTaskService.AddBgTask(b.GetGalgameInSourceTask());
    }
}

