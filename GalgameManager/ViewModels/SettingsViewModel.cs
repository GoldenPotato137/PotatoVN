using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;

using Microsoft.UI.Xaml;

using Windows.ApplicationModel;

using GalgameManager.Services;

using Microsoft.Windows.ApplicationModel.Resources;

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class SettingsViewModel : ObservableRecipient
{
    private readonly ILocalSettingsService _localSettingsService;
    private ElementTheme _elementTheme;
    private string _versionDescription;

    #region UI_STRINGS
    private static readonly ResourceLoader ResourceLoader= new();
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

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService)
    {
        var themeSelectorService1 = themeSelectorService;
        _elementTheme = themeSelectorService1.Theme;
        _versionDescription = GetVersionDescription();

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await themeSelectorService1.SetThemeAsync(param);
                }
            });

        _localSettingsService = localSettingsService;

        //RSS
        RssType = _localSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType).Result;
        IsSelectBangumi = RssType==RssType.Bangumi?Visibility.Visible:Visibility.Collapsed;
        BangumiToken = _localSettingsService.ReadSettingAsync<string>(KeyValues.BangumiToken).Result ?? "";
        //DOWNLOAD_BEHAVIOR
        _overrideLocalName = _localSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalName).Result;
        //CLOUD
        RemoteFolder = _localSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder).Result ?? "";
    }

    private static string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    #region RSS

    [ObservableProperty] private string _bangumiToken = string.Empty;
    [ObservableProperty] private RssType _rssType;
    [ObservableProperty] private Visibility _isSelectBangumi;
    [RelayCommand] private void RssSelectBangumi() => RssType = RssType.Bangumi;
    [RelayCommand] private void RssSelectVndb() => RssType = RssType.Vndb;
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

    #region CLOUD

    [ObservableProperty] private string? _remoteFolder;
    partial void OnRemoteFolderChanged(string? value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.RemoteFolder, value);
    }
    [RelayCommand]
    private async void SelectRemoteFolder()
    {
        RemoteFolder = await _localSettingsService.GetRemoteFolder(true);
    }

    #endregion
}
