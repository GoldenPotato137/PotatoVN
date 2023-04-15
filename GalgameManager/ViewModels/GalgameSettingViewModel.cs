using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;
using Windows.Storage.Pickers;

namespace GalgameManager.ViewModels;

public partial class GalgameSettingViewModel : ObservableRecipient, INavigationAware
{
    public Galgame Gal
    {
        get; set;
    }
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly List<RssType> RssTypes = new();

    private readonly GalgameCollectionService _galService;
    private readonly INavigationService _navigationService;
    [ObservableProperty] private bool _isPhrasing;

    public GalgameSettingViewModel(IDataCollectionService<Galgame> galCollectionService, INavigationService navigationService)
    {
        Gal = new Galgame();
        _galService = (GalgameCollectionService)galCollectionService;
        _navigationService = navigationService;
        RssTypes.Add(RssType.Bangumi);
        RssTypes.Add(RssType.Vndb);
    }

    public async void OnNavigatedFrom()
    {
        await _galService.SaveGalgamesAsync();
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not Galgame galgame)
        {
            return;
        }

        Gal = galgame;
        _galService.PhrasedEvent += UpdateIsPhrasing;
    }

    [RelayCommand]
    private void OnBack()
    {
        if(_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }

    [RelayCommand]
    private async void OnGetInfoFromRss()
    {
        IsPhrasing = true;
        await _galService.PhraseGalInfoAsync(Gal);
    }
    
    private void UpdateIsPhrasing()
    {
        IsPhrasing = _galService.IsPhrasing;
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        var openPicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".bmp");
        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            Gal.ImagePath.Value = file.Path;
        }
    }
}
