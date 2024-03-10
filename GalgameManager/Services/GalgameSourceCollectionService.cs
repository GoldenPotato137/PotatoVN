using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class GalgameSourceCollectionService : IDataCollectionService<IGalgameSource>
{
    private ObservableCollection<IGalgameSource> _galgameFolders = new();
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

    public async Task<ObservableCollection<IGalgameSource>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgameFolders;
    }

    public async Task InitAsync()
    {
        _galgameFolders = await _localSettingsService.ReadSettingAsync<ObservableCollection<IGalgameSource>>(KeyValues.GalgameFolders, true)
                          ?? new ObservableCollection<IGalgameSource>();
        List<Galgame> galgames = _galgameService.Galgames;

        await Task.Run(() =>
        {
            foreach (LocalFolderSource galgameFolder in _galgameFolders)
            {
                galgameFolder.GalgameService = _galgameService;
                foreach(Galgame game in galgames.Where(galgame => galgameFolder.IsInSource(galgame)))
                    galgameFolder.AddGalgame(game);
            }
        });
    }

    public Task StartAsync()
    {
        foreach (IGalgameSource source in _galgameFolders.Where(f => f.ScanOnStart))
        {
            if (source is LocalFolderSource localFolderSource)
            {
                _bgTaskService.AddBgTask(new GetGalgameInLocalFolderTask(localFolderSource));
            }
        }
        return Task.CompletedTask;
    }

    private async void OnGalgameAdded(Galgame galgame)
    {
        if (galgame.CheckExist() == false) return;
        try
        {
            await AddGalgameFolderAsync(galgame.Path[..galgame.Path.LastIndexOf('\\')], false);
        }
        catch (Exception)
        {
            // ignored
        }
        _galgameFolders.First(folder => folder.IsInSource(galgame)).AddGalgame(galgame);
    }

    private void OnGalgameDeleted(Galgame galgame)
    {
        _galgameFolders.FirstOrDefault(folder => folder.IsInSource(galgame))?.DeleteGalgame(galgame);
    }

    /// <summary>
    /// 试图添加一个galgame库
    /// </summary>
    /// <param name="path">库路径</param>
    /// <param name="tryGetGalgame">是否自动寻找库里游戏</param>
    /// <exception cref="Exception">库已经添加过了</exception>
    public async Task AddGalgameFolderAsync(string path, bool tryGetGalgame = true)
    {
        var url = $"local://{path}";
        if (_galgameFolders.Any(galFolder => galFolder.Url == url))
        {
            throw new Exception($"这个galgame库{path}已经添加过了");
        }

        LocalFolderSource localFolderSource = new(url, _galgameService);
        _galgameFolders.Add(localFolderSource);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameFolders, true);
        if (tryGetGalgame)
        {
            await _bgTaskService.AddBgTask(new GetGalgameInLocalFolderTask(localFolderSource));
        }
    }

    /// <summary>
    /// 删除一个galgame库，其包含弹窗警告，若用户取消则什么都不做
    /// </summary>
    /// <param name="localFolderSource"></param>
    public async Task DeleteGalgameFolderAsync(LocalFolderSource localFolderSource)
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
        
        if (delete == false || !_galgameFolders.Contains(localFolderSource)) return;
        foreach (Galgame galgame in await localFolderSource.GetGalgameList())
            await _galgameService.RemoveGalgame(galgame, true);
        _galgameFolders.Remove(localFolderSource);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameFolders, true);
    }
    
    /// <summary>
    /// 找到一个galgame库，若不存在则返回null
    /// </summary>
    public IGalgameSource? GetGalgameFolderFromUrl(string url)
    {
        return _galgameFolders.FirstOrDefault(folder => folder.Url == url);
    }

    /// <summary>
    /// 扫描所有库
    /// </summary>
    public void ScanAll()
    {
        foreach (IGalgameSource f in _galgameFolders)
        {
            if (f is LocalFolderSource localFolderSource)
            {
                _bgTaskService.AddBgTask(new GetGalgameInLocalFolderTask(localFolderSource));
            }
        }
    }
}

