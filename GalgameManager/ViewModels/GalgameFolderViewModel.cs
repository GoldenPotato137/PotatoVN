using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Windows.Storage.Pickers;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class GalgameFolderViewModel : ObservableObject, INavigationAware
{
    private readonly IDataCollectionService<GalgameFolder> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private GalgameFolder? _item;
    public ObservableCollection<Galgame> Galgames = new();
    
    [ObservableProperty]
    private bool _isInfoBarOpen;
    [ObservableProperty]
    private string _infoBarMessage = string.Empty;
    [ObservableProperty]
    private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

    public GalgameFolder? Item
    {
        get => _item;

        private set
        {
            SetProperty(ref _item, value);
            if (value != null)
                Galgames = value.GetGalgameList().Result;
        }
    }

    public GalgameFolderViewModel(IDataCollectionService<GalgameFolder> dataCollectionService, IDataCollectionService<Galgame> galgameService)
    {
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService) galgameService;
        _galgameService.GalgameAddedEvent += ReloadGalgameList;
    }

    private void ReloadGalgameList(Galgame galgame)
    {
        if (_item == null) return;
        if (galgame.Path.StartsWith(_item.Path))
            Galgames.Add(galgame);
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is string path)
        {
            var data = await _dataCollectionService.GetContentGridDataAsync();
            Item = data.First(i => i.Path == path);
        }
    }

    public void OnNavigatedFrom()
    {
    }

    [ICommand]
    private async void AddGalgame()
    {
        try
        {
            var openPicker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                if (folder.StartsWith(_item!.Path) == false)
                    throw new Exception("该游戏不属于这个库（游戏必须在库文件夹里面）");
                var result = await _galgameService.TryAddGalgameAsync(folder, true);
                if (result == GalgameCollectionService.AddGalgameResult.Success)
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = "已成功添加游戏到当前库";
                    InfoBarSeverity = InfoBarSeverity.Success;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
                else if (result == GalgameCollectionService.AddGalgameResult.AlreadyExists)
                    throw new Exception("库里已经有这个游戏了");
                else //NotFoundInRss
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = "成功添加游戏，但没有从信息源中找到这个游戏的信息";
                    InfoBarSeverity = InfoBarSeverity.Warning;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
            }
        }
        catch (Exception e)
        {
            IsInfoBarOpen = true;
            InfoBarMessage = e.Message;
            InfoBarSeverity = InfoBarSeverity.Error;
            await Task.Delay(3000);
            IsInfoBarOpen = false;
        }
    }
}
