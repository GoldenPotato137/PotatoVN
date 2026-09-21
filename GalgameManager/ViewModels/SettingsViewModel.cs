using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.EnumHelpers;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Store.Preview;
using Windows.Globalization;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using static System.String;
using AppInstance = Microsoft.Windows.AppLifecycle.AppInstance;

namespace GalgameManager.ViewModels;


public partial class SettingsViewModel : ObservableObject, INavigationAware
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly GalgameCollectionService _galgameCollectionService;
    private readonly INavigationService _navigationService;
    private readonly IUpdateService _updateService;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ICategoryService _categoryService;
    private readonly IInfoService _infoService;
    private readonly IBgTaskService _bgTaskService;
    private readonly ISidebarService _sidebarService;
    private string _versionDescription;

    #region UI_STRINGS //历史遗留，不要继续使用这种方式获取字符串

    private static readonly ResourceLoader ResourceLoader = new();
    public readonly string UiThemeTitle = ResourceLoader.GetString("SettingsPage_ThemeTitle");
    public readonly string UiThemeDescription = ResourceLoader.GetString("SettingsPage_ThemeDescription");
    public readonly string UiRssTitle = ResourceLoader.GetString("SettingsPage_RssTitle");
    public readonly string UiRssDescription = ResourceLoader.GetString("SettingsPage_RssDescription");
    public readonly string UiDownloadTitle = ResourceLoader.GetString("SettingsPage_DownloadTitle");
    public readonly string UiDownloadDescription = ResourceLoader.GetString("SettingsPage_DownloadDescription");
    public readonly string UiCloudSyncTitle = ResourceLoader.GetString("SettingsPage_CloudSyncTitle");
    public readonly string UiCloudSyncDescription = ResourceLoader.GetString("SettingsPage_CloudSyncDescription");
    public readonly string UiCloudSyncRoot = ResourceLoader.GetString("SettingsPage_CloudSync_Root");
    public readonly string UiLibraryTitle = "SettingsPage_LibraryTitle".GetLocalized();
    public readonly string UiLibraryDescription = "SettingsPage_LibraryDescription".GetLocalized();
    public readonly string UiLibraryMetaBackup = "SettingsPage_Library_MetaBackup".GetLocalized();
    public readonly string UiLibraryMetaBackupDescription = "SettingsPage_Library_MetaBackupDescription".GetLocalized();
    public readonly string UiLibrarySearchSubPath = "SettingsPage_Library_SearchSubPath".GetLocalized();
    public readonly string UiLibrarySearchSubPathDescription = "SettingsPage_Library_SearchSubPathDescription".GetLocalized();
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

    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    }

    public async void OnNavigatedTo(object parameter)
    {
        // 设置页会被 Frame 缓存；外部入口（例如单游戏按键映射对话框）修改总开关后，
        // 每次返回设置页都重新读取，避免界面状态与真实启动配置不一致。
        GameReMapEnabled = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GameReMapEnabled);
        PrecisePlayTime = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.PrecisePlayTime);
        try
        {
            await _updateService.UpdateSettingsBadgeAsync(); //通过这句话来触发更新弹窗提醒（如果这个版本没触发过的话）
            UpdateAvailable = await _updateService.IsUpdateAvailableAsync();
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Informational, ex.Message);
        }
    }

    public void OnNavigatedFrom()
    {
        _updateService.SettingBadgeEvent -= HandelSettingBadgeEvent;
        _galgameCollectionService.MetaSavedEvent -= SetSaveMetaPopUp;
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService,
        IGalgameCollectionService galgameService, IUpdateService updateService, INavigationService navigationService,
        ICategoryService categoryService, IInfoService infoService, IBgTaskService bgTaskService,
        ISidebarService sidebarService)
    {
        _categoryService = categoryService;
        _themeSelectorService = themeSelectorService;
        _navigationService = navigationService;
        _updateService = updateService;
        updateService.SettingBadgeEvent += HandelSettingBadgeEvent;
        _versionDescription = GetVersionDescription();
        _localSettingsService = localSettingsService;
        _infoService = infoService;
        _bgTaskService = bgTaskService;
        _sidebarService = sidebarService;

        //THEME
        _elementTheme = themeSelectorService.Theme;
        _language = _localSettingsService.ReadSettingAsync<LanguageEnum>(KeyValues.Language).Result;
        _backgroundMaterial = _localSettingsService.ReadSettingAsync<BackgroundMaterialEnum>(KeyValues.BackgroundMaterial).Result;
        _fixHorizontalPicture = _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result;
        TimeAsHour = _localSettingsService.ReadSettingAsync<bool>(KeyValues.TimeAsHour).Result;
        _transparentNavigationView = _localSettingsService.ReadSettingAsync<bool>(KeyValues.TransparentNavigationView).Result;
        _showGameNameInControl = _localSettingsService.ReadSettingAsync<bool>(KeyValues.ShowGameNameInControl).Result;
        _defaultGameName = _localSettingsService.ReadSettingAsync<DisplayName>(KeyValues.DefaultGameName).Result;
        _glassColor = TransparentGlassHelper.ParseHexColor(_localSettingsService.ReadSettingAsync<string>(KeyValues.GlassColor).Result);
        _glassAlpha = _localSettingsService.ReadSettingAsync<int>(KeyValues.GlassAlpha).Result;
        _CustomBackgroundEnabled = _localSettingsService.ReadSettingAsync<bool>(KeyValues.CustomBackgroundEnabled).Result;
        _useBannerAsBackground = _localSettingsService.ReadSettingAsync<bool>(KeyValues.UseBannerAsBackground).Result;
        _backgroundStretchMode = _localSettingsService.ReadSettingAsync<BackgroundStretchMode>(KeyValues.BackgroundStretchMode).Result;

        //GAME
        _recordOnlyForeground = _localSettingsService.ReadSettingAsync<bool>(KeyValues.RecordOnlyWhenForeground).Result;
        _precisePlayTime = _localSettingsService.ReadSettingAsync<bool>(KeyValues.PrecisePlayTime).Result;
        _playingWindowMode = _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode).Result;
        _minPlayTimeRecordThreshold = _localSettingsService.ReadSettingAsync<int>(KeyValues.MinPlayTimeRecordThreshold).Result;
        LocalEmulatorPath = _localSettingsService.ReadSettingAsync<string>(KeyValues.LocaleEmulatorPath).Result;
        _magpieTotalSwitch = _localSettingsService.ReadSettingAsync<bool>(KeyValues.MagpieTotalSwitch).Result;
        MagpiePath = _localSettingsService.ReadSettingAsync<string>(KeyValues.MagpiePath).Result; // Initialize MagpiePath
        _alwaysEnableMagpie = _localSettingsService.ReadSettingAsync<bool>(KeyValues.AlwaysEnableMagpie).Result;
        _alwaysMuteInBackground = _localSettingsService.ReadSettingAsync<bool>(KeyValues.AlwaysMuteInBackground).Result;
        _gameReMapEnabled = _localSettingsService.ReadSettingAsync<bool>(KeyValues.GameReMapEnabled).Result;
        _autoDetectSavePath = _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoDetectSavePath).Result;
        _magpieHotkeys = _localSettingsService.ReadSettingAsync<List<int>>(KeyValues.MagpieHotkeys).Result ?? [];
        UpdateMagpieHotkeysString();
        MagpieHotkeyKeys = new List<int>(_magpieHotkeys);
        PlayingWindowModes = new[] { WindowMode.Minimize, WindowMode.SystemTray, WindowMode.None };
        //RSS
        RssType = _localSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType).Result;
        _vndbTranslateTags = _localSettingsService.ReadSettingAsync<bool>(KeyValues.VndbTranslateTags).Result;
        _vndbCensorTags = _localSettingsService.ReadSettingAsync<bool>(KeyValues.VndbCensorTags).Result;
        _vndbRemoveSpoilerTags = _localSettingsService.ReadSettingAsync<bool>(KeyValues.VndbRemoveSpoilerTags).Result;
        _mixedPhraserTimeout = _localSettingsService.ReadSettingAsync<int>(KeyValues.MixedPhraserTimeout).Result;
        //DOWNLOAD_BEHAVIOR
        // _overrideLocalName = _localSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalName).Result;
        // _overrideLocalNameWithChinese = _localSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalNameWithChinese).Result;
        _autoCategory = _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoCategory).Result;
        _downloadPlayStatusWhenPhrasing = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncPlayStatusWhenPhrasing).Result;
        _downloadCharacters = _localSettingsService.ReadSettingAsync<bool>(KeyValues.DownloadCharacters).Result;
        //LIBRARY
        _galgameCollectionService = ((GalgameCollectionService?)galgameService)!;
        _galgameCollectionService.MetaSavedEvent += SetSaveMetaPopUp;
        _searchSubFolder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SearchChildFolder).Result;
        _metaBackup = false;
        _ignoreFetchResult = _localSettingsService.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult).Result;
        _regex = _localSettingsService.ReadSettingAsync<string>(KeyValues.RegexPattern).Result ?? ".+";
        _regexIndex = _localSettingsService.ReadSettingAsync<int>(KeyValues.RegexIndex).Result;
        _regexRemoveBorder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.RegexRemoveBorder).Result;
        _gameFolderMustContain = _localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderMustContain).Result ?? "";
        _gameFolderShouldContain = _localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderShouldContain).Result ?? "";
        //CLOUD
        RemoteFolder = _localSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder).Result ?? "";
        //QUICK_START
        _startPage = _localSettingsService.ReadSettingAsync<PageEnum>(KeyValues.StartPage).Result;
        QuitStart = _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart).Result;
        _authenticationType = _localSettingsService.ReadSettingAsync<AuthenticationType>(KeyValues.AuthenticationType).Result;
        _autoStartWhenLogin = _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoStartWhenLogin).Result;
        _minToTrayWhenAutoStart = _localSettingsService.ReadSettingAsync<bool>(KeyValues.MinToTrayWhenAutoStart).Result;
        //Notification
        NotifyWhenGetGalgameInFolder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder).Result;
        EventWhenGetGalgameInFolderEmpty = _localSettingsService.ReadSettingAsync<bool>(KeyValues.EventGetGalgameInFolderEmpty).Result;
        NotifyWhenUnpackGame = _localSettingsService.ReadSettingAsync<bool>(KeyValues.NotifyWhenUnpackGame).Result;
        _eventPvnSync = _localSettingsService.ReadSettingAsync<bool>(KeyValues.EventPvnSyncNotify).Result;
        _eventPvnSyncEmpty = _localSettingsService.ReadSettingAsync<bool>(KeyValues.EventPvnSyncEmptyNotify).Result;
        //Other
        UploadToAppCenter = _localSettingsService.ReadSettingAsync<bool>(KeyValues.UploadData).Result;
        MemoryImprove = _localSettingsService.ReadSettingAsync<bool>(KeyValues.MemoryImprove).Result;
        _fingerprintPlanEnabled = _localSettingsService.ReadSettingAsync<bool>(KeyValues.FingerprintPlanEnabled).Result;
        WindowModes = new[] { WindowMode.Normal, WindowMode.Close, WindowMode.SystemTray };
        CloseMode = _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.CloseMode).Result;
        DevelopmentMode = _localSettingsService.ReadSettingAsync<bool>(KeyValues.DevelopmentMode).Result;
        _isBetaChannel = _localSettingsService.ReadSettingAsync<bool>(KeyValues.IsBetaChannel).Result;
        _isAutoExportEnabled = _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoExport).Result;
        _autoExportInterval = _localSettingsService.ReadSettingAsync<double>(KeyValues.AutoExportInterval).Result;
        _autoExportPath = _localSettingsService.ReadSettingAsync<string>(KeyValues.AutoExportPath).Result ?? Empty;
        _lastExportTime = _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastExportTime).Result;
        _maxBackupNumber = _localSettingsService.ReadSettingAsync<int?>(KeyValues.MaxBackupNumber).Result ?? 999;
        UpdateAutoExportDescriptions();
        List<string> extensionsList = _localSettingsService.ReadSettingAsync<List<string>>(KeyValues.CustomTextFileExtensions).Result ?? [];
        _customTextFileExtensionsString = Join(", ", extensionsList);

        //Check the availability of Windows Hello
        UserConsentVerifierAvailability verifierAvailability = UserConsentVerifier.CheckAvailabilityAsync().AsTask().Result;
        AuthenticationTypes = verifierAvailability != UserConsentVerifierAvailability.Available
            ? new[] { AuthenticationType.NoAuthentication, AuthenticationType.CustomPassword }
            : new[] { AuthenticationType.NoAuthentication, AuthenticationType.WindowsHello, AuthenticationType.CustomPassword };
    }

    #region INFOBAR_CONTROL

    [ObservableProperty] private string _infoBarMsg = Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _isInfoBarOpen;
    private int _displayIndex;

    /// <summary>
    /// 使用InfoBar显示消息
    /// </summary>
    /// <param name="severity">严重程度</param>
    /// <param name="msg">消息本体</param>
    /// <param name="time">显示时间(ms)</param>
    private async Task DisplayMsgAsync(InfoBarSeverity severity, string msg, int time = 3000)
    {
        var index = ++_displayIndex;
        InfoBarSeverity = severity;
        InfoBarMsg = msg;
        IsInfoBarOpen = true;
        await Task.Delay(time);
        if (index == _displayIndex)
            IsInfoBarOpen = false;
    }

    #endregion

    #region UPDATE

    [ObservableProperty] private bool _updateAvailable;

    private async Task ShowUpdateNotification()
    {
        // 获取可用的更新版本
        var availableVersion = await _updateService.GetAvailableUpdateVersionAsync();
        if (availableVersion == null) return;

        // 显示更新确认对话框
        var userChoice = await _updateService.ShowUpdateConfirmationAsync();
        switch (userChoice)
        {
            case 1: // 立即更新
                await _updateService.PerformUpdateAsync();
                break;
            case 2: // 忽略这个版本
                await _updateService.IgnoreVersionAsync(availableVersion);
                _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Version_Ignored".GetLocalized());
                break;
            default:
                // 设置本次启动已取消更新标记，避免重复弹窗
                _updateService.SetUpdateCancelledThisSession();
                break;
        }
    }

    private async void HandelSettingBadgeEvent(bool result)
    {
        if (result == false) return;
        await ShowUpdateNotification();
        await _updateService.UpdateSettingsBadgeAsync();
    }

    #endregion

    #region THEME
    public readonly ElementTheme[] Themes = { ElementTheme.Default, ElementTheme.Light, ElementTheme.Dark };
    [ObservableProperty] private ElementTheme _elementTheme;
    [ObservableProperty] private bool _transparentNavigationView;

    public readonly LanguageEnum[] Languages =
    [
        LanguageEnum.Auto, LanguageEnum.ChineseSimplified, LanguageEnum.English, LanguageEnum.Japanese,
    ];
    [ObservableProperty] private LanguageEnum _language;

    /// <summary>
    /// 当前视图模型是否为中文环境（用于控制仅中文可见的设置）
    /// </summary>
    public bool IsChineseCulture =>
        Language == LanguageEnum.ChineseSimplified ||
        (Language == LanguageEnum.Auto &&
         System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));

    public readonly BackgroundStretchMode[] BackgroundStretchModes = { BackgroundStretchMode.Uniform, BackgroundStretchMode.Fill, BackgroundStretchMode.UniformToFill, BackgroundStretchMode.None };
    [ObservableProperty] private BackgroundStretchMode _backgroundStretchMode = BackgroundStretchMode.Uniform;

    public readonly BackgroundMaterialEnum[] BackgroundMaterials = { BackgroundMaterialEnum.Mica, BackgroundMaterialEnum.MicaAlt, BackgroundMaterialEnum.DesktopAcrylic, BackgroundMaterialEnum.Glass };
    [ObservableProperty] private BackgroundMaterialEnum _backgroundMaterial = BackgroundMaterialEnum.Mica;

    [ObservableProperty] private bool _CustomBackgroundEnabled;
    [ObservableProperty] private bool _useBannerAsBackground;
    [ObservableProperty] private int _glassAlpha = 5;
    [ObservableProperty] private Windows.UI.Color _glassColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    public bool IsGlassMaterial => BackgroundMaterial == BackgroundMaterialEnum.Glass;
    partial void OnBackgroundMaterialChanged(BackgroundMaterialEnum value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.BackgroundMaterial, value);
        _themeSelectorService.SetBackgroundMaterialAsync();
        OnPropertyChanged(nameof(IsGlassMaterial));
    }

    partial void OnGlassAlphaChanged(int value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.GlassAlpha, value);
        _themeSelectorService.SetBackgroundMaterialAsync();
    }

    partial void OnGlassColorChanged(Windows.UI.Color value)
    {
        var hex = $"{value.R:X2}{value.G:X2}{value.B:X2}";
        _localSettingsService.SaveSettingAsync(KeyValues.GlassColor, hex);
        _themeSelectorService.SetBackgroundMaterialAsync();
    }

    async partial void OnUseBannerAsBackgroundChanged(bool value)
    {
        await _localSettingsService.SaveSettingAsync(KeyValues.UseBannerAsBackground, value);
    }

    async partial void OnBackgroundStretchModeChanged(BackgroundStretchMode value)
    {
        await _localSettingsService.SaveSettingAsync(KeyValues.BackgroundStretchMode, value);
    }

    partial void OnTransparentNavigationViewChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.TransparentNavigationView, value);
        _infoService.Info(InfoBarSeverity.Informational,
            "SettingsPage_Theme_RestartRequired".GetLocalized(),
            displayTimeMs: 5000);

    }

    partial void OnElementThemeChanged(ElementTheme value)
    {
        _themeSelectorService.SetThemeAsync(value);
    }

    partial void OnLanguageChanged(LanguageEnum value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.Language, value);

        try
        {
            string languageTag = GetLanguageTag(value);
            ApplicationLanguages.PrimaryLanguageOverride = languageTag;

            // 提醒用户完全应用新语言还需要重启应用
            _infoService.Info(InfoBarSeverity.Informational,
                "SettingsPage_Language_RestartRequired".GetLocalized(),
                displayTimeMs: 5000);
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error,
                "SettingsPage_Language_ChangeError".GetLocalized(),
                ex.Message);
        }
        // 通知依赖语言环境的绑定项更新（例如仅在中文环境显示的选项）
        OnPropertyChanged(nameof(IsChineseCulture));
    }

    // 根据语言枚举获取对应的语言标记
    private string GetLanguageTag(LanguageEnum language)
    {
        return language switch
        {
            LanguageEnum.ChineseSimplified => "zh-CN",
            LanguageEnum.English => "en-US",
            LanguageEnum.Japanese => "ja-JP",
            LanguageEnum.Auto => "", // 空字符串表示使用系统默认语言
            _ => ""
        };
    }

    [ObservableProperty] private bool _fixHorizontalPicture;
    partial void OnFixHorizontalPictureChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.FixHorizontalPicture, value);

    // 时间显示单位改为小时
    [ObservableProperty] private bool _timeAsHour;
    partial void OnTimeAsHourChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.TimeAsHour, value);

    // 游戏控件是否显示游戏名称
    [ObservableProperty] private bool _showGameNameInControl;
    partial void OnShowGameNameInControlChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.ShowGameNameInControl, value);

    // 软件默认使用的游戏名
    public DisplayName[] DefaultGameNames { get; } = { DisplayName.ChineseName, DisplayName.OriginalName, DisplayName.Name };
    [ObservableProperty] private DisplayName _defaultGameName;

    /// <summary>
    /// 默认显示名称变更时批量更新游戏名字
    /// </summary>
    async partial void OnDefaultGameNameChanged(DisplayName value)
    {
        try
        {
            await _localSettingsService.SaveSettingAsync(KeyValues.DefaultGameName, value);
            // 根据枚举返回目标名字的局部函数
            async Task<string?> SelectNameAsync(Galgame g)
            {
                switch (value)
                {
                    case DisplayName.ChineseName:
                        if (!IsNullOrWhiteSpace(g.ChineseName.Value)) return g.ChineseName.Value;
                        goto case DisplayName.OriginalName;
                    case DisplayName.OriginalName:
                        if (!IsNullOrWhiteSpace(g.OriginalName.Value)) return g.OriginalName.Value;
                        goto case DisplayName.Name;
                    case DisplayName.Name:
                        return g.LocalPath is null
                            ? null
                            : await _galgameCollectionService.GetNameFromPath(GalgameSourceType.LocalZip, g.LocalPath);
                    default:
                        return null;
                }
            }

            IEnumerable<Task> saveTasks = _galgameCollectionService.Galgames.Select(async g =>
            {
                var newName = await SelectNameAsync(g);
                if (!IsNullOrEmpty(newName) && g.Name.Value != newName)
                {
                    g.Name.Value = newName;
                    await _galgameCollectionService.SaveGalgameAsync(g);
                }
            });

            await Task.WhenAll(saveTasks);
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    #endregion

    #region GAME

    [ObservableProperty] private bool _recordOnlyForeground;
    [ObservableProperty] private bool _precisePlayTime;
    [ObservableProperty] private WindowMode _playingWindowMode;
    [ObservableProperty] private int _minPlayTimeRecordThreshold;
    [ObservableProperty] private string? _localEmulatorPath;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(MagpieSettingVisible))] private bool _magpieTotalSwitch;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(MagpieSettingVisible))] private string? _magpiePath; // Magpie executable path
    public bool MagpieSettingVisible => MagpieTotalSwitch && !IsNullOrEmpty(MagpiePath);
    [ObservableProperty] private bool _alwaysEnableMagpie;
    [ObservableProperty] private bool _alwaysMuteInBackground;
    [ObservableProperty] private bool _gameReMapEnabled;
    [ObservableProperty] private bool _autoDetectSavePath;
    [ObservableProperty] private string _magpieHotkeysString = Empty;
    [ObservableProperty] private List<int> _magpieHotkeyKeys = new();
    private List<int> _magpieHotkeys;
    public WindowMode[] PlayingWindowModes;

    partial void OnRecordOnlyForegroundChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.RecordOnlyWhenForeground, value);

    partial void OnPrecisePlayTimeChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.PrecisePlayTime, value);

    partial void OnPlayingWindowModeChanged(WindowMode value) => _localSettingsService.SaveSettingAsync(KeyValues.PlayingWindowMode, value);

    partial void OnMinPlayTimeRecordThresholdChanged(int value) => _localSettingsService.SaveSettingAsync(KeyValues.MinPlayTimeRecordThreshold, value);

    partial void OnLocalEmulatorPathChanged(string? value) => _localSettingsService.SaveSettingAsync(KeyValues.LocaleEmulatorPath, value);

    partial void OnMagpieTotalSwitchChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.MagpieTotalSwitch, value);

    partial void OnMagpiePathChanged(string? value) => _localSettingsService.SaveSettingAsync(KeyValues.MagpiePath, value); // Save MagpiePath

    partial void OnAlwaysEnableMagpieChanged(bool value)
    {
        if (value && IsNullOrEmpty(MagpiePath))
        {
            _infoService.Info(InfoBarSeverity.Error, msg: "CallMagpieTask_NoMagpiePath".GetLocalized());
            AlwaysEnableMagpie = false;
            return;
        }
        _localSettingsService.SaveSettingAsync(KeyValues.AlwaysEnableMagpie, value);
    }

    partial void OnAlwaysMuteInBackgroundChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.AlwaysMuteInBackground, value);

    partial void OnGameReMapEnabledChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.GameReMapEnabled, value);

    partial void OnAutoDetectSavePathChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.AutoDetectSavePath, value);

    async partial void OnMagpieHotkeyKeysChanged(List<int> value)
    {
        try
        {
            if (value == null || value.Count == 0)
            {
                _magpieHotkeys = [(int)VirtualKey.LeftWindows, (int)VirtualKey.Shift, (int)VirtualKey.A];
                MagpieHotkeyKeys = new List<int>(_magpieHotkeys);
            }
            else
            {
                _magpieHotkeys = new List<int>(value);
            }

            await _localSettingsService.SaveSettingAsync(KeyValues.MagpieHotkeys, _magpieHotkeys);
            UpdateMagpieHotkeysString();
            _infoService.Info(InfoBarSeverity.Success, msg: "SettingSuccess".GetLocalized(), displayTimeMs: 2000);
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
            // 恢复到上次有效的值
            if (_magpieHotkeys != null)
            {
                MagpieHotkeyKeys = new List<int>(_magpieHotkeys);
            }
        }
    }

    async partial void OnMagpieHotkeysStringChanged(string value)
    {
        try
        {
            if (IsNullOrWhiteSpace(value))
            {
                _magpieHotkeys = [(int)VirtualKey.LeftWindows, (int)VirtualKey.Shift, (int)VirtualKey.A];
                UpdateMagpieHotkeysString(); // Reset to default string
                MagpieHotkeyKeys = new List<int>(_magpieHotkeys);
            }
            else
            {
                IEnumerable<string> keyStrings = value.Split('+').Select(s => s.Trim().ToLowerInvariant());
                List<int> newHotkeys = [];
                foreach (var keyString in keyStrings)
                {
                    if (IsNullOrEmpty(keyString)) continue;
                    if (Enum.TryParse(typeof(VirtualKey), keyString, true, out var virtualKey))
                    {
                        newHotkeys.Add((int)virtualKey);
                    }
                    else
                    {
                        // Try to parse modifier keys like "Win", "Shift", "Ctrl", "Alt"
                        switch (keyString)
                        {
                            case "win":
                            case "windows":
                            case "leftwindows":
                                newHotkeys.Add((int)VirtualKey.LeftWindows);
                                break;
                            case "shift":
                                newHotkeys.Add((int)VirtualKey.Shift);
                                break;
                            case "ctrl":
                            case "control":
                                newHotkeys.Add((int)VirtualKey.Control);
                                break;
                            case "alt":
                                newHotkeys.Add((int)VirtualKey.Menu); // Menu often represents alt
                                break;
                            default:
                                _infoService.Info(InfoBarSeverity.Error,
                                    msg: "SettingsPage_Game_MagpieHotkeysError".GetLocalized(keyString),
                                    displayTimeMs: 5000);
                                return;
                        }
                    }
                }
                _magpieHotkeys = newHotkeys;
                MagpieHotkeyKeys = new List<int>(_magpieHotkeys);
            }
            await _localSettingsService.SaveSettingAsync(KeyValues.MagpieHotkeys, _magpieHotkeys);
            _infoService.Info(InfoBarSeverity.Success, msg: "SettingSuccess".GetLocalized(), displayTimeMs: 2000);
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
            UpdateMagpieHotkeysString(); // Revert to current valid string on error
        }
    }

    private void UpdateMagpieHotkeysString()
    {
        MagpieHotkeysString = Join(" + ", _magpieHotkeys.Select(vk => ((VirtualKey)vk).ToString()));
    }

    [RelayCommand]
    private async Task ManageSidebarButtons()
    {
        SidebarButtonVisibilityDialog dialog = new(_sidebarService.GetButtons());
        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        _infoService.Info(InfoBarSeverity.Success, msg: "SettingSuccess".GetLocalized());
    }

    [RelayCommand]
    private async Task OpenGlobalKeyMappingDialog()
    {
        List<KeyMapping> globalKeyMappings;
        try
        {
            globalKeyMappings = await _localSettingsService.ReadSettingAsync<List<KeyMapping>>(KeyValues.GlobalKeyMappings) ?? new();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
            globalKeyMappings = new List<KeyMapping>();
        }

        GlobalKeyMappingDialog keyMappingDialog = new(globalKeyMappings)
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot
        };
        ContentDialogResult result = await keyMappingDialog.ShowAsync();

        // 只有在用户点击保存或确认清空时才保存设置
        if (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
        {
            await _localSettingsService.SaveSettingAsync(KeyValues.GlobalKeyMappings, keyMappingDialog.ResultMappings);
            KeyMappingTask[] runningTasks = _bgTaskService.GetBgTasks().OfType<KeyMappingTask>().ToArray();
            bool globalEnabled =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GameReMapEnabled);
            bool hasActiveRunningGames = runningTasks.Any(task =>
                globalEnabled || task.Galgame?.KeyReMap == true);
            string messageKey = hasActiveRunningGames
                ? "KeyMapping_Info_GlobalKeyMappingAppliedNow"
                : runningTasks.Length == 0 && globalEnabled
                    ? "KeyMapping_Info_GlobalKeyMappingSavedForNextLaunch"
                    : "KeyMapping_Info_GlobalKeyMappingSaved";
            _infoService.Info(InfoBarSeverity.Success,
                msg: messageKey.GetLocalized(),
                displayTimeMs: 3000);
        }
    }

    [RelayCommand]
    private async Task SelectLocalEmulatorPath()
    {
        FileOpenPicker openPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
            FileTypeFilter = { ".exe" }
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file?.Path != null)
        {
            LocalEmulatorPath = file.Path;
            _infoService.Info(InfoBarSeverity.Informational); //清除之前的消息
            if (file.Name != "LEProc.exe")
                _infoService.Info(InfoBarSeverity.Warning,
                    msg: "SettingsPage_Game_LocalEmulatorPathWarning".GetLocalized(), displayTimeMs: 5000);
        }
    }

    [RelayCommand]
    private async Task SelectMagpiePath()
    {
        FileOpenPicker openPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
            FileTypeFilter = { ".exe" }
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file?.Path != null)
        {
            MagpiePath = file.Path;
            _infoService.Info(InfoBarSeverity.Informational); // Clear previous messages
            if (file.Name != "Magpie.exe")
                _infoService.Info(InfoBarSeverity.Warning,
                    msg: "SettingsPage_Game_MagpiePathWarning".GetLocalized(), displayTimeMs: 5000);
            _infoService.Info(InfoBarSeverity.Success, msg: "SettingsPage_Game_MagpiePath_Success".GetLocalized());
        }
    }

    [ObservableProperty]
    private string _customTextFileExtensionsString;

    async partial void OnCustomTextFileExtensionsStringChanged(string value)
    {
        try
        {
            List<string> extensionsList = value.Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(ext => ext.Trim())
                .Where(ext => !IsNullOrWhiteSpace(ext))
                .ToList();
            for (var i = 0; i < extensionsList.Count; i++)
                if (!extensionsList[i].StartsWith("."))
                    extensionsList[i] = "." + extensionsList[i];
            await _localSettingsService.SaveSettingAsync(KeyValues.CustomTextFileExtensions, extensionsList);
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    #endregion

    #region RSS

    [ObservableProperty] private RssType _rssType;
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly List<RssType> RssTypes = RssHelperX.GetAvailableTypes(App.GetService<IGalgameCollectionService>());
    [ObservableProperty] private bool _vndbTranslateTags;
    [ObservableProperty] private bool _vndbCensorTags;
    [ObservableProperty] private bool _vndbRemoveSpoilerTags;
    [ObservableProperty] private int _mixedPhraserTimeout;

    partial void OnRssTypeChanged(RssType value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.RssType, value);
    }

    partial void OnMixedPhraserTimeoutChanged(int value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.MixedPhraserTimeout, value);

    [RelayCommand]
    public async Task SetMixedPhraserOrderAsync()
    {
        MixedPhraserOrder order = (await _localSettingsService.
            ReadSettingAsync<MixedPhraserOrder>(KeyValues.MixedPhraserOrder))!;
        MixedPhraserOrderDialog dialog = new(order);
        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        await _localSettingsService.SaveSettingAsync(KeyValues.MixedPhraserOrder, order);
    }

    [RelayCommand]
    public async Task SetMixedPhraserEnabledAsync()
    {
        MixedPhraserEnabled enabled = await _localSettingsService.
            ReadSettingAsync<MixedPhraserEnabled>(KeyValues.MixedPhraserEnabled) ?? new MixedPhraserEnabled();
        MixedPhraserEnabledDialog dialog = new(enabled);
        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        await _localSettingsService.SaveSettingAsync(KeyValues.MixedPhraserEnabled, dialog.Result);
    }


    #endregion

    #region DOWNLOAD_BEHAVIOR

    // [ObservableProperty] private bool _overrideLocalName;
    // [ObservableProperty] private bool _overrideLocalNameWithChinese;
    [ObservableProperty] private bool _autoCategory;
    [ObservableProperty] private bool _downloadPlayStatusWhenPhrasing;
    [ObservableProperty] private bool _downloadCharacters;

    // partial void OnOverrideLocalNameChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.OverrideLocalName, value);

    // partial void OnOverrideLocalNameWithChineseChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.OverrideLocalNameWithChinese, value);

    partial void OnAutoCategoryChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.AutoCategory, value);

    partial void OnDownloadPlayStatusWhenPhrasingChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.SyncPlayStatusWhenPhrasing, value);

    partial void OnDownloadCharactersChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.DownloadCharacters, value);

    partial void OnVndbTranslateTagsChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.VndbTranslateTags, value);

    partial void OnVndbCensorTagsChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.VndbCensorTags, value);

    partial void OnVndbRemoveSpoilerTagsChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.VndbRemoveSpoilerTags, value);

    [RelayCommand]
    private async Task CategoryNow()
    {
        await _categoryService.UpdateAllGames();
    }

    [RelayCommand]
    private async Task DownloadPlayStatusFormBgmNow()
    {
        _ = DisplayMsgAsync(InfoBarSeverity.Informational, "HomePage_Downloading".GetLocalized(), 1000 * 120);
        (GalStatusSyncResult, string) result = await _galgameCollectionService.DownloadAllPlayStatus(RssType.Bangumi);
        await DisplayMsgAsync(result.Item1.ToInfoBarSeverity(), result.Item2);
    }

    [RelayCommand]
    private async Task DownloadPlayStatusFormVndbNow()
    {
        _ = DisplayMsgAsync(InfoBarSeverity.Informational, "HomePage_Downloading".GetLocalized(), 1000 * 120);
        (GalStatusSyncResult, string) result = await _galgameCollectionService.DownloadAllPlayStatus(RssType.Vndb);
        await DisplayMsgAsync(result.Item1.ToInfoBarSeverity(), result.Item2);
    }

    [RelayCommand]
    private async Task UploadAllPlayStatus()
    {
        try
        {
            UploadAllPlayStatusDialog dialog = new();
            ContentDialogResult dlgResult = await dialog.ShowAsync();
            if (dlgResult != ContentDialogResult.Primary || dialog.Canceled) return;

            bool uploadBangumi = dialog.SelectedBangumi;
            bool uploadVndb = dialog.SelectedVndb;

            if (!uploadBangumi && !uploadVndb)
            {
                _infoService.Info(InfoBarSeverity.Warning, msg: "UploadAllPlayStatusDialog_NoTargetSelected".GetLocalized());
                return;
            }

            if (_bgTaskService.GetBgTask<UploadAllPlayStatusTask>(Empty) is not null)
            {
                _infoService.Info(InfoBarSeverity.Warning, msg: "UploadAllPlayStatusTask_AlreadyRunning".GetLocalized());
                return;
            }

            UploadAllPlayStatusTask task = _bgTaskService.CreateBgTask<UploadAllPlayStatusTask>(uploadBangumi, uploadVndb);
            await _bgTaskService.AddBgTask(task);
            _infoService.Info(InfoBarSeverity.Success, msg: "UploadAllPlayStatusTask_Started".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    #endregion

    #region LIBRARY

    [ObservableProperty] private bool _metaBackup;
    [ObservableProperty] private string _metaBackupProgress = "";
    [ObservableProperty] private string _removeMetaBackupProgress = Empty;
    [ObservableProperty] private bool _searchSubFolder;
    [ObservableProperty] private bool _ignoreFetchResult;
    [ObservableProperty] private string _regex;
    [ObservableProperty] private int _regexIndex;
    [ObservableProperty] private bool _regexRemoveBorder;
    [ObservableProperty] private string _gameFolderMustContain;
    [ObservableProperty] private string _gameFolderShouldContain;
    [ObservableProperty] private string _regexTryItOut = "";

    partial void OnMetaBackupChanged(bool value)
    {
        IGalgameSourceCollectionService sourceCollectionService = App.GetService<IGalgameSourceCollectionService>();
        foreach (GalgameSourceBase source in sourceCollectionService.GetGalgameSources())
        {
            source.SaveMetaBackup = value;
            sourceCollectionService.Save(source);
        }
    }
    partial void OnSearchSubFolderChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.SearchChildFolder, value);
    partial void OnIgnoreFetchResultChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.IgnoreFetchResult, value);

    partial void OnRegexChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexPattern, value);

    partial void OnRegexIndexChanged(int value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexIndex, value);

    partial void OnRegexRemoveBorderChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexRemoveBorder, value);

    partial void OnGameFolderShouldContainChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.GameFolderShouldContain, value);

    partial void OnGameFolderMustContainChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.GameFolderMustContain, value);

    [RelayCommand]
    private void OnRegexTryItOut() => RegexTryItOut = NameRegex.GetName(RegexTryItOut, Regex, RegexRemoveBorder, RegexIndex);

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

    [RelayCommand]
    private async Task RemoveMetaBackUp()
    {
        foreach (Galgame game in _galgameCollectionService.Galgames)
            foreach (GalgameSourceBase source in game.Sources)
            {
                RemoveMetaBackupProgress = "SettingsPage_Library_RemoveMetaBackupProgress".GetLocalized(game.Name.Value ?? Empty);
                await SourceServiceFactory.GetSourceService(source.SourceType).RemoveMetaAsync(game);
            }
        RemoveMetaBackupProgress = "Done!";
    }

    #endregion

    #region CLOUD

    [ObservableProperty] private string? _remoteFolder;
    partial void OnRemoteFolderChanged(string? value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.RemoteFolder, value);
    }
    [RelayCommand]
    private async Task SelectRemoteFolder()
    {
        await Task.CompletedTask;
        var suggestedPath = RemoteFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        PvnFolderPicker picker = new()
        {
            Title = "SettingsViewModel_SelectRemoteFolder_Title".GetLocalized(),
            OkButtonLabel = "SettingsViewModel_SelectRemoteFolder_Choose".GetLocalized(),
            InitialDirectory = suggestedPath,
        };
        PickerResult result = picker.ShowDialog();
        RemoteFolder = result == PickerResult.OK ? picker.SelectedPath : suggestedPath;
    }

    #endregion

    #region QUIT_START

    [ObservableProperty] private bool _quitStart;
    public readonly PageEnum[] StartPages = { PageEnum.Home, PageEnum.Category, PageEnum.MultiStream };
    [ObservableProperty] private PageEnum _startPage;
    public readonly AuthenticationType[] AuthenticationTypes;
    [ObservableProperty] private AuthenticationType _authenticationType;
    [ObservableProperty] private bool _autoStartWhenLogin;
    [ObservableProperty] private bool _minToTrayWhenAutoStart;
    public bool IsMSIXPackaged => RuntimeHelper.IsMSIX;

    partial void OnQuitStartChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.QuitStart, value);

    partial void OnStartPageChanged(PageEnum value) => _localSettingsService.SaveSettingAsync(KeyValues.StartPage, value);

    async partial void OnAuthenticationTypeChanged(AuthenticationType value)
    {
        switch (value)
        {
            case AuthenticationType.NoAuthentication:
            case AuthenticationType.WindowsHello:
                break;
            case AuthenticationType.CustomPassword:
                var result = await TrySetCustomPassword();
                if (!result)
                {
                    AuthenticationType = AuthenticationType.NoAuthentication;
                    return;
                }
                break;
        }

        await _localSettingsService.SaveSettingAsync(KeyValues.AuthenticationType, value);
    }

    private async Task<bool> TrySetCustomPassword()
    {
        PasswordDialog passwordDialog = new()
        {
            Title = "SetYourPasswordLiteral".GetLocalized(),
            Message = "SaveYourPasswordCarefullyLiteral".GetLocalized(),
            PrimaryButtonText = "ConfirmLiteral".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            PasswordBoxPlaceholderText = "PasswordLiteral".GetLocalized(),
        };
        await passwordDialog.ShowAsync();

        var password = passwordDialog.Password;
        if (IsNullOrEmpty(password) is not true)
        {
            PasswordCredential credential = new(KeyValues.CustomPasswordSaverName, KeyValues.CustomPasswordDisplayName, password);
            new PasswordVault().Add(credential);
            return true;
        }
        else
        {
            return false;
        }
    }

    partial void OnAutoStartWhenLoginChanged(bool value)
    {
        Task.Run(async () =>
        {
            try
            {
                StartupTask startupTask = await StartupTask.GetAsync("PotatoVNStartup");
                if (value && (startupTask.State is StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy))
                {
                    if (startupTask.State == StartupTaskState.DisabledByUser)
                        _infoService.Info(InfoBarSeverity.Warning, "SettingsPage_Start_AutoStartDisabledByUser".GetLocalized());
                    else if (startupTask.State == StartupTaskState.DisabledByPolicy)
                        _infoService.Info(InfoBarSeverity.Warning, "SettingsPage_Start_AutoStartDisabledByPolicy".GetLocalized());
                    await UiThreadInvokeHelper.InvokeAsync(() => { AutoStartWhenLogin = false; });
                    return;
                }

                if (value) await startupTask.RequestEnableAsync();
                else startupTask.Disable();
                await _localSettingsService.SaveSettingAsync(KeyValues.AutoStartWhenLogin, value);
            }
            catch (Exception e)
            {
                _infoService.Info(InfoBarSeverity.Error, "SettingsPage_Start_AutoStartFail".GetLocalized(), e.Message);
            }
        });
    }

    partial void OnMinToTrayWhenAutoStartChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.MinToTrayWhenAutoStart, value);

    [RelayCommand]
    private async Task CreateDesktopShortcut()
    {
        bool isPinnedSuccessfully = false;
        string shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "PotatoVN.lnk");

        if (File.Exists(shortcutPath))
        {
            await DisplayMsgAsync(InfoBarSeverity.Informational, "SettingsPage_Start_DesktopShortcut_AlreadyExists".GetLocalized());
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                // 有包身份时优先走 StoreConfiguration
                if (RuntimeHelper.IsMSIX)
                {
                    var pkgFamilyName = Package.Current.Id.FamilyName;

                    if (StoreConfiguration.IsPinToDesktopSupported())
                    {
                        StoreConfiguration.PinToDesktop(pkgFamilyName);
                        isPinnedSuccessfully = true;
                        return;
                    }
                }

                // 无包身份/不支持 PinToDesktop：直接创建 .lnk
                var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "GalgameManager.exe");
                var workDir = AppContext.BaseDirectory;
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new Exception("WScript.Shell is not available");
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = workDir;
                shortcut.Description = "PotatoVN";
                if (File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath;
                }
                shortcut.Save();
                isPinnedSuccessfully = true;
            }
            catch (Exception e)
            {
                _infoService.Info(InfoBarSeverity.Error, "SettingsPage_Start_DesktopShortcut_Fail".GetLocalized(), e.Message);
            }
        });

        if (isPinnedSuccessfully)
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");

                if (File.Exists(iconPath) && File.Exists(shortcutPath))
                {
                    // 使用PowerShell命令修改快捷方式图标
                    string command = $@"$shell = New-Object -ComObject WScript.Shell; " +
                                    $@"$shortcut = $shell.CreateShortcut('{shortcutPath.Replace("\\", "\\\\")}'); " +
                                    $@"$shortcut.IconLocation = '{iconPath.Replace("\\", "\\\\")}'; " +
                                    $@"$shortcut.Save()";

                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-Command \"{command}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                }
                await DisplayMsgAsync(InfoBarSeverity.Success, "SettingsPage_Start_DesktopShortcut_Success".GetLocalized());
            }
            catch (Exception ex)
            {
                _infoService.Info(InfoBarSeverity.Warning, "SettingsPage_Start_DesktopShortcut_IconFail".GetLocalized(),
                    ex.Message);
            }
        }
    }

    #endregion

    #region Other

    [ObservableProperty] private bool _uploadToAppCenter;
    [ObservableProperty] private bool _fingerprintPlanEnabled;
    [ObservableProperty] private bool _memoryImprove;
    [ObservableProperty] private WindowMode _closeMode;
    [ObservableProperty] private bool _developmentMode;
    [ObservableProperty] private bool _isBetaChannel;
    [ObservableProperty] private bool _isAutoExportEnabled;
    [ObservableProperty] private double _autoExportInterval;
    [ObservableProperty] private string _autoExportIntervalDescription = string.Empty;
    [ObservableProperty] private string _autoExportPathDescription = string.Empty;
    [ObservableProperty] private int _maxBackupNumber;
    private string _autoExportPath;
    private DateTime _lastExportTime;
    public bool IsSideloadVersion => !App.IsStoreVersion();
    public readonly WindowMode[] WindowModes;

    partial void OnUploadToAppCenterChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.UploadData, value);

    async partial void OnFingerprintPlanEnabledChanged(bool value)
    {
        try
        {
            await _localSettingsService.SaveSettingAsync(KeyValues.FingerprintPlanEnabled, value);
            if (!value) return;
            if (_bgTaskService.GetBgTask<FingerprintUploadTask>(Empty) is not null) return;
            await _bgTaskService.AddBgTask(new FingerprintUploadTask());
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    partial void OnMemoryImproveChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.MemoryImprove, value);

    partial void OnCloseModeChanged(WindowMode value) => _localSettingsService.SaveSettingAsync(KeyValues.CloseMode, value);

    partial void OnDevelopmentModeChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.DevelopmentMode, value);

    partial void OnIsBetaChannelChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.IsBetaChannel, value);
        _infoService.Info(InfoBarSeverity.Success, msg: "SettingsPage_Other_BetaChannel_Success".GetLocalized());
    }

    async partial void OnIsAutoExportEnabledChanged(bool value)
    {
        try
        {
            if (!value) return;
            if (IsNullOrEmpty(_autoExportPath) || !Directory.Exists(_autoExportPath))
            {
                _infoService.Info(InfoBarSeverity.Error, msg: "SettingsPage_Other_AutoExport_PathInvalid".GetLocalized());
                IsAutoExportEnabled = false;
                return;
            }
            await _localSettingsService.SaveSettingAsync(KeyValues.AutoExport, value);
            _infoService.Info(InfoBarSeverity.Success, msg: "SettingSuccess".GetLocalized() + ", " + "RestartRequired".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    partial void OnAutoExportIntervalChanged(double value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.AutoExportInterval, value);
        UpdateAutoExportDescriptions();
    }

    partial void OnMaxBackupNumberChanged(int value) => _localSettingsService.SaveSettingAsync(KeyValues.MaxBackupNumber, value);

    private void UpdateAutoExportDescriptions()
    {
        AutoExportIntervalDescription = "SettingsPage_Other_AutoExport_Interval_Description".GetLocalized(_lastExportTime);
        AutoExportPathDescription = IsNullOrEmpty(_autoExportPath)
            ? "SettingsPage_Other_AutoExport_Path_NotSet".GetLocalized()
            : "SettingsPage_Other_AutoExport_Path_Description".GetLocalized(_autoExportPath);
    }

    [RelayCommand]
    private async Task SelectAutoExportPath()
    {
        FolderPicker openPicker = new();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.SuggestedStartLocation = PickerLocationId.HomeGroup;
        openPicker.FileTypeFilter.Add("*");
        StorageFolder? folder = await openPicker.PickSingleFolderAsync();
        if (folder is not null)
        {
            _autoExportPath = folder.Path;
            UpdateAutoExportDescriptions();
            await _localSettingsService.SaveSettingAsync(KeyValues.AutoExportPath, _autoExportPath);
            _infoService.Info(InfoBarSeverity.Success, "SettingSuccess".GetLocalized());
        }
    }

    [RelayCommand]
    private async Task ExportData()
    {
        try
        {
            if (_bgTaskService.GetBgTask<ExportTask>(Empty) is not null)
            {
                _infoService.Info(InfoBarSeverity.Warning, "SettingsPage_Other_Export_Exporting".GetLocalized());
                return;
            }

            FolderPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
            openPicker.SuggestedStartLocation = PickerLocationId.HomeGroup;
            openPicker.FileTypeFilter.Add("*");
            StorageFolder? folder = await openPicker.PickSingleFolderAsync();
            var path = folder?.Path;
            if (path is null) return;

            ExportTask task = new(path);
            await _bgTaskService.AddBgTask(task);
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "SettingsPage_Other_Export_Fail".GetLocalized(),
                $"{e.Message}\n{e.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task ImportData()
    {
        try
        {
            if (_bgTaskService.GetBgTask<ExportTask>(Empty) is not null)
            {
                _infoService.Info(InfoBarSeverity.Warning, "SettingsPage_Other_Export_Exporting".GetLocalized());
                return;
            }

            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
            openPicker.SuggestedStartLocation = PickerLocationId.HomeGroup;
            openPicker.FileTypeFilter.Add(".zip");
            StorageFile file = await openPicker.PickSingleFileAsync();
            var path = file?.Path;
            if (path is null) return;
            if (file?.Name.EndsWith("pvnExport.zip") is not true)
                throw new PvnException("SettingsPage_Other_Import_WrongFile".GetLocalized());

            _infoService.Info(InfoBarSeverity.Success, msg: "SettingsPage_Other_Import_Copying".GetLocalized());
            await Task.Run(() =>
            {
                File.Copy(path, Path.Combine(_localSettingsService.LocalFolder.FullName, file.Name));
            });
            _infoService.Info(InfoBarSeverity.Success, msg: "SettingsPage_Other_Import_Restart".GetLocalized(),
                displayTimeMs: 1000 * 10);
            await Task.Delay(10 * 1000);
            AppInstance.Restart("/import"); // 并没有实现这个参数，只是为了和正常启动区分
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "SettingsPage_Other_Import_Fail".GetLocalized(),
                $"{e.Message}\n{e.StackTrace}");
        }
    }

    #endregion

    #region Notification

    [ObservableProperty] private bool _notifyWhenGetGalgameInFolder;
    [ObservableProperty] private bool _eventWhenGetGalgameInFolderEmpty;
    [ObservableProperty] private bool _notifyWhenUnpackGame;
    [ObservableProperty] private bool _eventPvnSync;
    [ObservableProperty] private bool _eventPvnSyncEmpty;

    partial void OnNotifyWhenGetGalgameInFolderChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.NotifyWhenGetGalgameInFolder, value);

    partial void OnNotifyWhenUnpackGameChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.NotifyWhenUnpackGame, value);

    partial void OnEventPvnSyncChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.EventPvnSyncNotify, value);
        if (!value)
            EventPvnSyncEmpty = false;
    }

    partial void OnEventPvnSyncEmptyChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.EventPvnSyncEmptyNotify, value);

    partial void OnEventWhenGetGalgameInFolderEmptyChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.EventGetGalgameInFolderEmpty, value);

    #endregion

    #region ABOUT
    [RelayCommand]
    private async Task CheckForUpdates()
    {
        var availableVersion = await _updateService.GetAvailableUpdateVersionAsync();
        if (availableVersion != null)
        {
            var userChoice = await _updateService.ShowUpdateConfirmationAsync();
            switch (userChoice)
            {
                case 1: // 立即更新
                    await _updateService.PerformUpdateAsync();
                    break;
                case 2: // 忽略这个版本
                    await _updateService.IgnoreVersionAsync(availableVersion);
                    _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Version_Ignored".GetLocalized());
                    break;
                default:
                    // 设置本次启动已取消更新标记，避免重复弹窗
                    _updateService.SetUpdateCancelledThisSession();
                    break;
            }
        }
        else
        {
            _infoService.Info(InfoBarSeverity.Informational, "UpdateService_NoUpdate_Title".GetLocalized(), "UpdateService_NoUpdate_Content".GetLocalized());
        }
    }

    [RelayCommand]
    private async Task OpenUpdateWeb()
    {
        var storeUrl = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateUrl) ??
                       "https://apps.microsoft.com/detail/9p9cbkd5hr3w";
        await Launcher.LaunchUriAsync(new Uri(storeUrl));
    }

    [RelayCommand]
    private async Task Rate()
    {
        StoreContext context = StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, App.MainWindow!.GetWindowHandle());
        await context.RequestRateAndReviewAppAsync();
    }

    [RelayCommand]
    private void UpdateContent()
    {
        _navigationService.NavigateTo(typeof(UpdateContentViewModel).FullName!);
    }

    private static string GetVersionDescription()
    {
        return $"{"AppDisplayName".GetLocalized()} - {RuntimeHelper.GetVersion()}";
    }

    #endregion
    //选择，清理自定义图片
    [RelayCommand]
    private async Task PickCustomBackground()
    {
        var picker = new FileOpenPicker();
        var hwnd = App.MainWindow!.GetWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await ShellPage.SetCustomBackgroundAsync(file.Path);
        }
    }

    [RelayCommand]
    private async Task ClearCustomBackground()
    {
        await ShellPage.ClearCustomBackgroundAsync();
    }
}
