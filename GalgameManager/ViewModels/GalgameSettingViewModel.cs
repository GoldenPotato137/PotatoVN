using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.ViewModels;

public partial class GalgameSettingViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private Galgame _gal = null!;
    public List<RssType> RssTypes { get; }= new() { RssType.Bangumi, RssType.Vndb, RssType.Mixed, RssType.Ymgal };

    private readonly GalgameCollectionService _galService;
    private readonly INavigationService _navigationService;
    private readonly IPvnService _pvnService;
    private readonly string[] _searchUrlList = new string[Galgame.PhraserNumber];
    [ObservableProperty] private string _searchUri = "";
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private RssType _selectedRss = RssType.None;
    [ObservableProperty] private string _lastFetchInfoStr = string.Empty;

    public GalgameSettingViewModel(IGalgameCollectionService galCollectionService, INavigationService navigationService,
        IPvnService pvnService)
    {
        Gal = new Galgame();
        _galService = (GalgameCollectionService)galCollectionService;
        _navigationService = navigationService;
        _pvnService = pvnService;
        _searchUrlList[(int)RssType.Bangumi] = "https://bgm.tv/subject_search/";
        _searchUrlList[(int)RssType.Vndb] = "https://vndb.org/v/all?sq=";
        _searchUrlList[(int)RssType.Mixed] = "https://bgm.tv/subject_search/";
        _searchUrlList[(int)RssType.Ymgal] = "https://www.ymgal.games/search?type=ga&keyword=";
        SearchUri = _searchUrlList[(int)RssType.Vndb]; // default
    }

    public async void OnNavigatedFrom()
    {
        if (Gal.ImagePath.Value != Galgame.DefaultImagePath && !File.Exists(Gal.ImagePath.Value))
            Gal.ImagePath.Value = Galgame.DefaultImagePath;
        await _galService.SaveGalgamesAsync(Gal);
        _pvnService.Upload(Gal, PvnUploadProperties.Infos | PvnUploadProperties.ImageLoc);
        _galService.PhrasedEvent -= Update;
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not Galgame galgame)
        {
            return;
        }

        Gal = galgame;
        SelectedRss = Gal.RssType;
        _galService.PhrasedEvent += Update;
        Update();
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

    private void Update()
    {
        IsPhrasing = _galService.IsPhrasing;
        LastFetchInfoStr = "GalgameSettingPage_LastFetchInfoTime".GetLocalized(
            new DateTimeToStringConverter().Convert(Gal.LastFetchInfoTime, default!, default!, default!));
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".bmp");
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file == null) return;
        StorageFile newFile = await file.CopyAsync(await FileHelper.GetFolderAsync(FileHelper.FolderType.Images), 
            $"{Gal.Name.Value}{file.FileType}", NameCollisionOption.ReplaceExisting);
        Gal.ImagePath.Value= newFile.Path;
    }
}
