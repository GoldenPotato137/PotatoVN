using System.Windows.Input;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace GalgameManager.ViewModels;

public partial class SettingsViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly GalgameCollectionService _galgameCollectionService;
    private readonly INavigationService _navigationService;
    private readonly IUpdateService _updateService;
    private ElementTheme _elementTheme;
    private string _versionDescription;

    #region UI_STRINGS

    private static readonly ResourceLoader ResourceLoader = new();
    public readonly string UiThemeTitle = ResourceLoader.GetString("SettingsPage_ThemeTitle");
    public readonly string UiThemeDescription = ResourceLoader.GetString("SettingsPage_ThemeDescription");
    public readonly string UiRssTitle = ResourceLoader.GetString("SettingsPage_RssTitle");
    public readonly string UiRssDescription = ResourceLoader.GetString("SettingsPage_RssDescription");
    public readonly string UiRssBgmPlaceholder = ResourceLoader.GetString("SettingsPage_Rss_BgmPlaceholder");
    public readonly string UiDownloadTitle = ResourceLoader.GetString("SettingsPage_DownloadTitle");
    public readonly string UiDownloadDescription = ResourceLoader.GetString("SettingsPage_DownloadDescription");
    public readonly string UiDownLoadOverrideNameTitle = ResourceLoader.GetString("SettingsPage_Download_OverrideNameTitle");
    public readonly string UiDownLoadOverrideNameDescription = ResourceLoader.GetString("SettingsPage_Download_OverrideNameDescription");
    public readonly string UiCloudSyncTitle = ResourceLoader.GetString("SettingsPage_CloudSyncTitle");
    public readonly string UiCloudSyncDescription = ResourceLoader.GetString("SettingsPage_CloudSyncDescription");
    public readonly string UiCloudSyncRoot = ResourceLoader.GetString("SettingsPage_CloudSync_Root");
    public readonly string UiSelect = ResourceLoader.GetString("Select");
    public readonly string UiAbout = ResourceLoader.GetString("Settings_AboutDescription").Replace("\\n", "\n");
    public readonly string UiQuickStartTitle = "SettingsPage_QuickStartTitle".GetLocalized();
    public readonly string UiQuickStartDescription = "SettingsPage_QuickStartDescription".GetLocalized();
    public readonly string UiQuickStartAutoStartGameTitle = "SettingsPage_QuickStart_AutoStartGameTitle".GetLocalized();
    public readonly string UiQuickStartAutoStartGameDescription = "SettingsPage_QuickStart_AutoStartGameDescription".GetLocalized();
    public readonly string UiLibraryTitle = "SettingsPage_LibraryTitle".GetLocalized();
    public readonly string UiLibraryDescription = "SettingsPage_LibraryDescription".GetLocalized();
    public readonly string UiLibraryMetaBackup = "SettingsPage_Library_MetaBackup".GetLocalized();
    public readonly string UiLibraryMetaBackupDescription = "SettingsPage_Library_MetaBackupDescription".GetLocalized();
    public readonly string UiLibrarySearchSubPath = "SettingsPage_Library_SearchSubPath".GetLocalized();
    public readonly string UiLibrarySearchSubPathDescription = "SettingsPage_Library_SearchSubPathDescription".GetLocalized();
    public readonly string UiLibrarySearchSubPathDepth = "SettingsPage_Library_SearchSubPathDepth".GetLocalized();
    public readonly string UiLibrarySearchSubPathDepthDescription = "SettingsPage_Library_SearchSubPathDepthDescription".GetLocalized();
    public readonly string UiLibraryNameDescription = "SettingsPage_Library_NameDescription".GetLocalized();
    public readonly string UiLibrarySearchRegex = "SettingsPage_Library_SearchRegex".GetLocalized();
    public readonly string UiLibrarySearchRegexDescription = "SettingsPage_Library_SearchRegexDescription".GetLocalized();
    public readonly string UiLibrarySearchRegexIndex = "SettingsPage_Library_SearchRegexIndex".GetLocalized();
    public readonly string UiLibrarySearchRegexIndexDescription = "SettingsPage_Library_SearchRegexIndexDescription".GetLocalized();
    public readonly string UiLibrarySearchRegexRemoveBorder = "SettingsPage_Library_SearchRegexRemoveBorder".GetLocalized();
    public readonly string UiLibrarySearchRegexRemoveBorderDescription = "SettingsPage_Library_SearchRegexRemoveBorderDescription".GetLocalized();
    public readonly string UiLibrarySearchRegexTryItOut = "SettingsPage_Library_SearchRegexTryItOut".GetLocalized();
    public readonly string UiLibraryGameSearchRule = "SettingsPage_Library_GameSearchRule".GetLocalized();
    public readonly string UiLibraryGameSearchRuleDescription = "SettingsPage_Library_GameSearchRuleDescription".GetLocalized();
    public readonly string UiLibraryGameSearchRuleMustContain = "SettingsPage_Library_GameSearchRuleMustContain".GetLocalized();
    public readonly string UiLibraryGameSearchRuleShouldContain = "SettingsPage_Library_GameSearchRuleShouldContain".GetLocalized();

    #endregion

    public ElementTheme ElementTheme
    {
        get => _elementTheme;
        set => SetProperty(ref _elementTheme, value);
    }

    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    }

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (_shouldDisplayUpdateNotification)
        {
            await ShowUpdateNotification();
            await _updateService.UpdateSettingsBadgeAsync();
        }
    }

    public void OnNavigatedFrom() { }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService, 
        IDataCollectionService<Galgame> galgameService, IUpdateService updateService, INavigationService navigationService)
    {
        _navigationService = navigationService;
        _updateService = updateService;
        updateService.SettingBadgeEvent += result => _shouldDisplayUpdateNotification = result;
        updateService.UpdateSettingsBadgeAsync(); //只是为了触发事件，原地TP，先这么写吧
        var themeSelectorService1 = themeSelectorService;
        _elementTheme = themeSelectorService1.Theme;
        _versionDescription = GetVersionDescription();

        async void Execute(ElementTheme param)
        {
            if (ElementTheme != param)
            {
                ElementTheme = param;
                await themeSelectorService1.SetThemeAsync(param);
            }
        }

        SwitchThemeCommand = new RelayCommand<ElementTheme>(Execute);

        _localSettingsService = localSettingsService;
        //THEME
        _fixHorizontalPicture = _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result;
        //RSS
        RssType = _localSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType).Result;
        IsSelectBangumi = RssType == RssType.Bangumi ? Visibility.Visible : Visibility.Collapsed;
        BangumiToken = _localSettingsService.ReadSettingAsync<string>(KeyValues.BangumiToken).Result ?? "";
        //DOWNLOAD_BEHAVIOR
        _overrideLocalName = _localSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalName).Result;
        //LIBRARY
        _galgameCollectionService = ((GalgameCollectionService?)galgameService)!;
        _galgameCollectionService.MetaSavedEvent += SetSaveMetaPopUp;
        _metaBackup = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SaveBackupMetadata).Result;
        _searchSubFolder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SearchChildFolder).Result;
        _searchSubFolderDepth = _localSettingsService.ReadSettingAsync<int>(KeyValues.SearchChildFolderDepth).Result;
        _ignoreFetchResult = _localSettingsService.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult).Result;
        _regex = _localSettingsService.ReadSettingAsync<string>(KeyValues.RegexPattern).Result ?? ".+";
        _regexIndex = _localSettingsService.ReadSettingAsync<int>(KeyValues.RegexIndex).Result;
        _regexRemoveBorder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.RegexRemoveBorder).Result;
        _gameFolderMustContain = _localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderMustContain).Result ?? "";
        _gameFolderShouldContain = _localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderShouldContain).Result ?? "";
        //CLOUD
        RemoteFolder = _localSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder).Result ?? "";
        //QUICK_START
        QuitStart = _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart).Result;
    }

    #region UPDATE
    
    private bool _shouldDisplayUpdateNotification;
    
    private async Task ShowUpdateNotification()
    {
        ContentDialog updateDialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "SettingsPage_UpdateNotification_Title".GetLocalized(),
            Content = "SettingsPage_UpdateNotification_Msg".GetLocalized(),
            PrimaryButtonText = "SettingsPage_SeeWhatsNew".GetLocalized(),
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary
        };
        updateDialog.PrimaryButtonClick += (_, _) =>
            _navigationService.NavigateTo(typeof(UpdateContentViewModel).FullName!);
        await _localSettingsService.SaveSettingAsync(KeyValues.LastNoticeUpdateVersion, RuntimeHelper.GetVersion());
        await updateDialog.ShowAsync();
    }
    
    #endregion

    #region THEME

    [ObservableProperty] private bool _fixHorizontalPicture;
    partial void OnFixHorizontalPictureChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.FixHorizontalPicture, value);

    #endregion

    #region RSS

    [ObservableProperty] private string _bangumiToken = string.Empty;
    [ObservableProperty] private RssType _rssType;
    [ObservableProperty] private Visibility _isSelectBangumi;
    [RelayCommand]
    private void RssSelectBangumi() => RssType = RssType.Bangumi;
    [RelayCommand]
    private void RssSelectVndb() => RssType = RssType.Vndb;
    partial void OnRssTypeChanged(RssType value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.RssType, value);
        IsSelectBangumi = value == RssType.Bangumi ? Visibility.Visible : Visibility.Collapsed;
    }

    partial void OnBangumiTokenChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.BangumiToken, value);

    #endregion

    #region DOWNLOAD_BEHAVIOR

    [ObservableProperty] private bool _overrideLocalName;

    partial void OnOverrideLocalNameChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.OverrideLocalName, value);

    #endregion

    #region LIBRARY

    [ObservableProperty] private bool _metaBackup;
    [ObservableProperty] private string _metaBackupProgress = "";
    [ObservableProperty] private bool _searchSubFolder;
    [ObservableProperty] private int _searchSubFolderDepth;
    [ObservableProperty] private bool _ignoreFetchResult;
    [ObservableProperty] private string _regex;
    [ObservableProperty] private int _regexIndex;
    [ObservableProperty] private bool _regexRemoveBorder;
    [ObservableProperty] private string _gameFolderMustContain;
    [ObservableProperty] private string _gameFolderShouldContain;
    [ObservableProperty] private string _regexTryItOut = "";

    partial void OnMetaBackupChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.SaveBackupMetadata, value);

    partial void OnSearchSubFolderChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.SearchChildFolder, value);

    partial void OnSearchSubFolderDepthChanged(int value) => _localSettingsService.SaveSettingAsync(KeyValues.SearchChildFolderDepth, value);

    partial void OnIgnoreFetchResultChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.IgnoreFetchResult, value);
    
    partial void OnRegexChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexPattern, value);

    partial void OnRegexIndexChanged(int value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexIndex, value);

    partial void OnRegexRemoveBorderChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexRemoveBorder, value);

    partial void OnGameFolderShouldContainChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.GameFolderShouldContain, value);

    partial void OnGameFolderMustContainChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.GameFolderMustContain, value);

    [RelayCommand]
    private void OnRegexTryItOut() => RegexTryItOut = NameRegex.GetName(_regexTryItOut, _regex, _regexRemoveBorder, _regexIndex);

    private void SetSaveMetaPopUp(Galgame galgame)
    {
        MetaBackupProgress = "SettingsPage_Library_MetaBackupProgress".GetLocalized() + galgame.Name.Value;
    }

    [RelayCommand]
    private async Task SaveMetaBackUp()
    {
        await _galgameCollectionService.SaveAllMetaAsync();
        MetaBackupProgress = "Done!";
    }

    #endregion

    #region CLOUD

    [ObservableProperty] private string? _remoteFolder;
    partial void OnRemoteFolderChanged(string? value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.RemoteFolder, value);
    }
    [RelayCommand]
    private async void SelectRemoteFolder()
    {
        FolderPicker openPicker = new();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
        openPicker.SuggestedStartLocation = PickerLocationId.HomeGroup;
        openPicker.FileTypeFilter.Add("*");
        StorageFolder? folder = await openPicker.PickSingleFolderAsync();
        RemoteFolder = folder?.Path;
    }

    #endregion

    #region QUIT_START

    [ObservableProperty] private bool _quitStart;

    partial void OnQuitStartChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.QuitStart, value);

    #endregion

    #region ABOUT

    [RelayCommand]
    private async void Rate()
    {
        StoreContext context = StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, App.MainWindow.GetWindowHandle());
        await context.RequestRateAndReviewAppAsync();
    }
    
    private static string GetVersionDescription()
    {
        return $"{"AppDisplayName".GetLocalized()} - {RuntimeHelper.GetVersion()}";
    }

    #endregion
}
