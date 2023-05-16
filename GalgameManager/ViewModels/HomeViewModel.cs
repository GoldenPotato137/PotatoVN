using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class HomeViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Visibility _fixHorizontalPicture;

    #region UI

    private static readonly ResourceLoader ResourceLoader= new();
    public readonly string UiEdit = ResourceLoader.GetString("HomePage_Edit");
    public readonly string UiDownLoad = ResourceLoader.GetString("HomePage_Download");
    public readonly string UiRemove = ResourceLoader.GetString("HomePage_Remove");
    public readonly string UiAddNewGame = ResourceLoader.GetString("HomePage_AddNewGame");
    public readonly string UiSort = ResourceLoader.GetString("HomePage_Sort");
    public readonly string UiFilter = ResourceLoader.GetString("HomePage_Filter");
    
    private readonly string _uiAddGameSuccess = ResourceLoader.GetString("HomePage_AddGameSuccess");
    private readonly string _uiAlreadyInLibrary = ResourceLoader.GetString("HomePage_AlreadyInLibrary");
    private readonly string _uiNoInfo = ResourceLoader.GetString("HomePage_NoInfo");
    private readonly string _uiRemoveTitle = ResourceLoader.GetString("HomePage_Remove_Title");
    private readonly string _uiRemoveMessage = ResourceLoader.GetString("HomePage_Remove_Message");
    private readonly string _uiYes = ResourceLoader.GetString("Yes");
    private readonly string _uiCancel = ResourceLoader.GetString("Cancel");

    #endregion

    public ICommand ItemClickCommand
    {
        get;
    }

    public ObservableCollection<Galgame> Source { get; private set; } = new();

    public HomeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> dataCollectionService,
        ILocalSettingsService localSettingsService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)_dataCollectionService;
        
        ((GalgameCollectionService)dataCollectionService).GalgameLoadedEvent += async () => Source = await dataCollectionService.GetContentGridDataAsync();
        _galgameService.PhrasedEvent += () => IsPhrasing = false;
        // IsPhrasing = _galgameService.IsPhrasing;

        _fixHorizontalPicture = localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result 
                                == false ? Visibility.Collapsed : Visibility.Visible;

        ItemClickCommand = new RelayCommand<Galgame>(OnItemClick);
    }

    public async void OnNavigatedTo(object parameter)
    {
        // var newItems = await _dataCollectionService.GetContentGridDataAsync();
        // Source.Clear();
        // foreach (var item in newItems)
        // {
        //     Source.Add(item);
        // }
        Source = await _dataCollectionService.GetContentGridDataAsync();
    }

    public void OnNavigatedFrom()
    {
    }

    private void OnItemClick(Galgame? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, clickedItem.Path);
        }
    }

    [RelayCommand]
    private async void AddGalgame()
    {
        try
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                IsPhrasing = true;
                GalgameCollectionService.AddGalgameResult result = await _galgameService.TryAddGalgameAsync(folder, true);
                if (result == GalgameCollectionService.AddGalgameResult.Success)
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = _uiAddGameSuccess;
                    InfoBarSeverity = InfoBarSeverity.Success;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
                else if (result == GalgameCollectionService.AddGalgameResult.AlreadyExists)
                {
                    throw new Exception(_uiAlreadyInLibrary);
                }
                else //NotFoundInRss
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = _uiNoInfo;
                    InfoBarSeverity = InfoBarSeverity.Warning;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
            }
        }
        catch (Exception e)
        {
            IsPhrasing = false;
            IsInfoBarOpen = true;
            InfoBarMessage = e.Message;
            InfoBarSeverity = InfoBarSeverity.Error;
            await Task.Delay(3000);
            IsInfoBarOpen = false;
        }
    }
    
    [RelayCommand]
    private async void Sort()
    {
        await _galgameService.SetSortKeysAsync();
    }

    [RelayCommand]
    private async void Search(object et)
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async void GalFlyOutDelete(Galgame? galgame)
    {
        if(galgame == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = _uiRemoveTitle,
            Content = _uiRemoveMessage,
            PrimaryButtonText = _uiYes,
            SecondaryButtonText = _uiCancel
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(galgame);
        };
        
        await dialog.ShowAsync();
    }
    
    [RelayCommand]
    private void GalFlyOutEdit(Galgame? galgame)
    {
        if(galgame == null) return;
        _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame);
    }

    [RelayCommand]
    private async void GalFlyOutGetInfoFromRss(Galgame? galgame)
    {
        if(galgame == null) return;
        IsPhrasing = true;
        await _galgameService.PhraseGalInfoAsync(galgame);
        IsPhrasing = false;
    }
}
