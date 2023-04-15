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

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class SettingsViewModel : ObservableRecipient
{
    private readonly ILocalSettingsService _localSettingsService;
    private ElementTheme _elementTheme;
    private string _versionDescription;

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
        BangumiToken = _localSettingsService.ReadSettingAsync<string>(KeyValues.BangumiToken).Result ?? "";
        //DOWNLOAD_BEHAVIOR
        _overrideLocalName = _localSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalName).Result;
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
}
