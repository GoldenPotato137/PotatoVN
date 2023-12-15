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
    
    [ObservableProperty]
    private bool _isInfoBarOpen;

    [ObservableProperty]
    private string _infoBarMessage = string.Empty;
 
    [ObservableProperty]
    private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    
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
            IsInfoBarOpen = true;
            InfoBarMessage = e.Message;
            InfoBarSeverity = InfoBarSeverity.Error;
            await Task.Delay(3000);
            IsInfoBarOpen = false;
        }
    }

    [RelayCommand]
    private async Task DeleteFolder(GalgameFolder? galgameFolder)
    {
        if (galgameFolder == null) return;
        await _galFolderService.DeleteGalgameFolderAsync(galgameFolder);
    }
}
