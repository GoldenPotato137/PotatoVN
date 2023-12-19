using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class LibraryViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly GalgameFolderCollectionService _galFolderService;
    
    public ObservableCollection<GalgameFolder> Source { get; private set; } = new();
    public ICommand ItemClickCommand { get; }
    public ICommand AddLibraryCommand { get; }
    
    #region UI

    public readonly string UiDeleteFolder = "LibraryPage_DeleteFolder".GetLocalized();

    #endregion

    public LibraryViewModel(INavigationService navigationService, IDataCollectionService<GalgameFolder> galFolderService)
    {
        _navigationService = navigationService;
        _galFolderService = (GalgameFolderCollectionService) galFolderService;

        ItemClickCommand = new RelayCommand<GalgameFolder>(OnItemClick);
        AddLibraryCommand = new RelayCommand(AddLibrary);
    }

    public async void OnNavigatedTo(object parameter)
    {
        Source = await _galFolderService.GetContentGridDataAsync();
    }

    public void OnNavigatedFrom(){}
    
    private void OnItemClick(GalgameFolder? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.NavigateTo(typeof(GalgameFolderViewModel).FullName!, clickedItem.Path);
        }
    }

    private async void AddLibrary()
    {
        try
        {
            FolderPicker folderPicker = new();
            folderPicker.FileTypeFilter.Add("*");

            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                await _galFolderService.AddGalgameFolderAsync(folder.Path);
            }
        }
        catch (Exception e)
        {
            _ = DisplayMsgAsync(InfoBarSeverity.Error, e.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteFolder(GalgameFolder? galgameFolder)
    {
        if (galgameFolder == null) return;
        await _galFolderService.DeleteGalgameFolderAsync(galgameFolder);
    }
    
    [RelayCommand]
    private void ScanAll()
    {
        _galFolderService.ScanAll();
        _ = DisplayMsgAsync(InfoBarSeverity.Success, "LibraryPage_ScanAll_Success".GetLocalized(Source.Count));
    }
    
    #region INFO_BAR_CTRL

    private int _infoBarIndex;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

    /// <summary>
    /// 使用InfoBar显示信息
    /// </summary>
    /// <param name="infoBarSeverity">信息严重程度</param>
    /// <param name="msg">信息</param>
    /// <param name="delayMs">显示时长(ms)</param>
    private async Task DisplayMsgAsync(InfoBarSeverity infoBarSeverity, string msg, int delayMs = 3000)
    {
        var currentIndex = ++_infoBarIndex;
        InfoBarSeverity = infoBarSeverity;
        InfoBarMessage = msg;
        IsInfoBarOpen = true;
        await Task.Delay(delayMs);
        if (currentIndex == _infoBarIndex)
            IsInfoBarOpen = false;
    }

    #endregion
}
