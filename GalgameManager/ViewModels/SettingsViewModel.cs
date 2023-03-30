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
public class SettingsViewModel : ObservableRecipient
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

    public ICommand OnFinishBgmTokenInputCommand => new RelayCommand(OnFinishBgmTokenInput);

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
        
        var currentRss = _localSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType).Result;
        _infoSettingSelected[(int)currentRss] = true;
        _bangumiToken = _localSettingsService.ReadSettingAsync<string>(KeyValues.BangumiToken).Result ?? "";
    }

    private async void OnFinishBgmTokenInput()
    {
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiToken, _bangumiToken);
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
    
    private readonly bool[] _infoSettingSelected = new bool[3]; //信息源：表示第几个选项有没有被选中 
    private string _bangumiToken;

    public bool RssSelectBangumi
    {
        get => _infoSettingSelected[(int)RssType.Bangumi];

        set
        {
            SetProperty(ref _infoSettingSelected[(int)RssType.Bangumi], value);
            if (value)
                _localSettingsService.SaveSettingAsync(KeyValues.RssType, RssType.Bangumi);
        }
    }
    
    public bool RssSelectBilibili
    {
        get => _infoSettingSelected[(int)RssType.Bilibili];
        set
        {
            SetProperty(ref _infoSettingSelected[(int)RssType.Bilibili], value);
            if (value)
                _localSettingsService.SaveSettingAsync(KeyValues.RssType, RssType.Bilibili);
        }
    }
    
    public bool RssSelectMoegirl
    {
        get => _infoSettingSelected[(int)RssType.Moegirl];
        set
        {
            SetProperty(ref _infoSettingSelected[(int)RssType.Moegirl], value);
            if (value)
                _localSettingsService.SaveSettingAsync(KeyValues.RssType, RssType.Moegirl);
        }
    }
    
    public string BangumiToken
    {
        get => _bangumiToken;
        set
        {
            SetProperty(ref _bangumiToken, value);
            OnFinishBgmTokenInputCommand.Execute(null);
        }
    }
}
