using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Services;

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
    private readonly IPvnService _pvnService;
    private readonly string[] _searchUrlList = new string[5];
    [ObservableProperty] private string _searchUri = "";
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private RssType _selectedRss = RssType.None;

    public GalgameSettingViewModel(IDataCollectionService<Galgame> galCollectionService, INavigationService navigationService,
        IPvnService pvnService)
    {
        Gal = new Galgame();
        _galService = (GalgameCollectionService)galCollectionService;
        _navigationService = navigationService;
        _pvnService = pvnService;
        RssTypes.Add(RssType.Bangumi);
        RssTypes.Add(RssType.Vndb);
        RssTypes.Add(RssType.Mixed);
        _searchUrlList[(int)RssType.Bangumi] = "https://bgm.tv/subject_search/";
        _searchUrlList[(int)RssType.Vndb] = "https://vndb.org/v/all?sq=";
        _searchUrlList[(int)RssType.Mixed] = "https://bgm.tv/subject_search/";
        SearchUri = _searchUrlList[(int)RssType.Vndb]; // default
    }

    public async void OnNavigatedFrom()
    {
        if (Gal.ImagePath.Value != Galgame.DefaultImagePath && File.Exists(Gal.ImagePath.Value) == false)
            Gal.ImagePath.Value = Galgame.DefaultImagePath;
        await _galService.SaveGalgamesAsync(Gal);
        _pvnService.Upload(Gal, PvnUploadProperties.Infos | PvnUploadProperties.ImageLoc);
        _galService.PhrasedEvent -= UpdateIsPhrasing;
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not Galgame galgame)
        {
            return;
        }

        Gal = galgame;
        SelectedRss = Gal.RssType;
        _galService.PhrasedEvent += UpdateIsPhrasing;
    }

    partial void OnSelectedRssChanged(RssType value)
    {
        Gal.RssType = value;
        SearchUri = _searchUrlList[(int)value] + Gal.Name.Value;
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
    private async Task OnGetInfoFromRss()
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
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
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
