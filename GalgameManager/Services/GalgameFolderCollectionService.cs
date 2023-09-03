using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;
using SharpCompress;

namespace GalgameManager.Services;

public class GalgameFolderCollectionService : IDataCollectionService<GalgameFolder>
{
    private ObservableCollection<GalgameFolder> _galgameFolders = new();
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;

    public GalgameFolderCollectionService(ILocalSettingsService localSettingsService, IDataCollectionService<Galgame> galgameService)
    {
        _localSettingsService = localSettingsService;
        _galgameService = ((GalgameCollectionService?)galgameService)!;
        _galgameService.GalgameAddedEvent += OnGalgameAdded;
        _galgameService.GalgameDeletedEvent += OnGalgameDeleted;
    }

    public async Task<ObservableCollection<GalgameFolder>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgameFolders;
    }

    public async Task InitAsync()
    {
        _galgameFolders = await _localSettingsService.ReadSettingAsync<ObservableCollection<GalgameFolder>>(KeyValues.GalgameFolders, true)
                          ?? new ObservableCollection<GalgameFolder>();
        ObservableCollection<Galgame> galgames = await _galgameService.GetContentGridDataAsync();

        await Task.Run(() =>
        {
            foreach (GalgameFolder galgameFolder in _galgameFolders)
            {
                galgameFolder.GalgameService = _galgameService;
                galgames.Where(galgame => galgameFolder.IsInFolder(galgame)).ForEach(galgameFolder.AddGalgame);
            }
        });
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
        _galgameFolders.First(folder => folder.IsInFolder(galgame)).AddGalgame(galgame);
    }

    private void OnGalgameDeleted(Galgame galgame)
    {
        _galgameFolders.FirstOrDefault(folder => folder.IsInFolder(galgame))?.DeleteGalgame(galgame);
    }

    /// <summary>
    /// 试图添加一个galgame库
    /// </summary>
    /// <param name="path">库路径</param>
    /// <param name="tryGetGalgame">是否自动寻找库里游戏</param>
    /// <exception cref="Exception">库已经添加过了</exception>
    public async Task AddGalgameFolderAsync(string path, bool tryGetGalgame = true)
    {
        if (_galgameFolders.Any(galFolder => galFolder.Path == path))
        {
            throw new Exception($"这个galgame库{path}已经添加过了");
        }

        GalgameFolder galgameFolder = new(path, _galgameService);
        _galgameFolders.Add(galgameFolder);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameFolders, true);
        if (tryGetGalgame)
        {
            await galgameFolder.GetGalgameInFolder(_localSettingsService);
        }
    }

    /// <summary>
    /// 删除一个galgame库，其包含弹窗警告，若用户取消则什么都不做
    /// </summary>
    /// <param name="galgameFolder"></param>
    public async Task DeleteGalgameFolderAsync(GalgameFolder galgameFolder)
    {
        var delete = false;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "GalgameFolderCollectionService_DeleteGalgameFolderAsync_Title".GetLocalized(),
            Content = "GalgameFolderCollectionService_DeleteGalgameFolderAsync_Content".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            PrimaryButtonCommand = new RelayCommand(() => delete = true),
            DefaultButton = ContentDialogButton.Secondary
        };
        await dialog.ShowAsync();
        
        if (delete == false || !_galgameFolders.Contains(galgameFolder)) return;
        foreach (Galgame galgame in await galgameFolder.GetGalgameList())
            await _galgameService.RemoveGalgame(galgame);
        _galgameFolders.Remove(galgameFolder);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameFolders, true);
    }
}

