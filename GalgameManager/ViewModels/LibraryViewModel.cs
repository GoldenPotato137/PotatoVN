using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class LibraryViewModel : ObservableObject, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly GalgameSourceService _galSourceServiceService;
    
    public ObservableCollection<GalgameSourceBase> Source { get; private set; } = new();
    public ICommand ItemClickCommand { get; }
    public ICommand AddLibraryCommand { get; }
    
    #region UI

    public readonly string UiDeleteFolder = "LibraryPage_DeleteFolder".GetLocalized();

    #endregion

    public LibraryViewModel(INavigationService navigationService, IGalgameSourceService galFolderService)
    {
        _navigationService = navigationService;
        _galSourceServiceService = (GalgameSourceService) galFolderService;

        ItemClickCommand = new RelayCommand<GalgameSourceBase>(OnItemClick);
        AddLibraryCommand = new RelayCommand(AddLibrary);
    }

    public async void OnNavigatedTo(object parameter)
    {
        Source = await _galSourceServiceService.GetGalgameSourcesAsync();
    }

    public void OnNavigatedFrom(){}
    
    private void OnItemClick(GalgameSourceBase? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.NavigateTo(typeof(GalgameSourceViewModel).FullName!, clickedItem.Url);
        }
    }

    private async void AddLibrary()
    {
        try
        {
            AddSourceDialog dialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
            };
            await dialog.ShowAsync();
            if (dialog.Canceled) return;
            switch (dialog.SelectItem)
            {
                case 0:
                    await _galSourceServiceService.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, dialog.Path);
                    break;
                case 1:
                    await _galSourceServiceService.AddGalgameSourceAsync(GalgameSourceType.LocalZip, dialog.Path);
                    break;
            }

        }
        catch (Exception e)
        {
            _ = DisplayMsgAsync(InfoBarSeverity.Error, e.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteFolder(GalgameSourceBase? galgameFolder)
    {
        if (galgameFolder is null) return;
        await _galSourceServiceService.DeleteGalgameFolderAsync(galgameFolder);
    }
    
    [RelayCommand]
    private void ScanAll()
    {
        _galSourceServiceService.ScanAll();
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
