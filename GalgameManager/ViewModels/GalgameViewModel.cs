using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using GalgameManager.Views.GalgamePagePanel;
using GalgameManager.WinApp.Base.Contracts.PluginUi;
using GalgameManager.WinApp.Base.Models;

namespace GalgameManager.ViewModels;

public partial class GalgameViewModel : ObservableObject, INavigationAware
{
    private readonly GalgameCollectionService _galgameService;
    private readonly GalgameSourceCollectionService _sourceService;
    private readonly IStaffService _staffService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IPvnService _pvnService;
    private readonly IInfoService _infoService;
    private readonly IPluginService _pluginService;
    private readonly IGameLaunchService _gameLaunchService; // 按明确安装实例启动游戏
    [ObservableProperty] private Galgame? _item;
    /// <summary>
    /// 当前逻辑游戏安装实例的界面集合，仅供界面读取。
    /// </summary>
    public ObservableCollection<GalgameAndPath> Installations { get; } = [];
    /// <summary>
    /// 当前首选安装实例的配置。
    /// </summary>
    public LocalInstallationConfig? CurrentInstallationConfig =>
        Item?.PreferredLocalInstallation?.LocalConfig;
    /// <summary>
    /// 当前游戏是否存在多个安装实例。
    /// </summary>
    public bool HasMultipleInstallations => Installations.Count > 1;
    public ObservableCollection<GamePanelBase> Panels { get; } = [];
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeSavePositionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetExePathCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFromDiskCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetLocalPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedThreadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectProcessCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectTextCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearTextCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetPathCommand))]
    [ObservableProperty] private bool _isLocalGame; //是否是本地游戏（而非云端同步过来/本地已删除的虚拟游戏）
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private bool _hasSaveDirectory; //是否有检测到或设置的存档目录
    [ObservableProperty] private Visibility _showSingleExplorerButton = Visibility.Visible; //是否显示单个打开按钮
    [ObservableProperty] private Visibility _showExplorerMenu = Visibility.Collapsed; //是否显示打开菜单

    [ObservableProperty] private Visibility _isRemoveSelectedThreadVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isSelectProcessVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isResetPathVisible = Visibility.Collapsed;
    [ObservableProperty] private bool _canOpenInBgm;
    [ObservableProperty] private bool _canOpenInVndb;
    [ObservableProperty] private bool _canOpenInYmgal;
    [ObservableProperty] private bool _canOpenInCngal;
    [ObservableProperty] private bool _canOpenInHikarinagi;
    [ObservableProperty] private bool _canOpenInSteam;
    [ObservableProperty] private bool _canOpenInExternalWebsite;
    [ObservableProperty] private Visibility _showBackgroundImage = Visibility.Collapsed;
    [ObservableProperty] private Visibility _showTagPanel = Visibility.Collapsed;
    [ObservableProperty] private Visibility _showCharacterPanel = Visibility.Collapsed;
    [ObservableProperty] private Visibility _showDescriptionPanel = Visibility.Visible;
    private bool IsNotLocalGame => !IsLocalGame;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(SettingAvailable))] private IGalgamePage? _plugin; //第三方游戏详情页插件
    private PluginInfo? _pluginInfo;
    public bool SettingAvailable => Plugin is null or IGalgamePageSetting;
    [ObservableProperty] private UIElement? _pluginUi;
    public ObservableCollection<UIElement> LeftPanelPluginUis { get; } = [];
    public ObservableCollection<UIElement> RightPanelPluginUis { get; } = [];

    public GalgameViewModel(IGalgameCollectionService dataCollectionService, IStaffService staffService,
        INavigationService navigationService, ILocalSettingsService localSettingsService,
        IPvnService pvnService, IInfoService infoService, IGalgameSourceCollectionService sourceService,
        IPluginService pluginService, IGameLaunchService gameLaunchService)
    {
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _sourceService = (GalgameSourceCollectionService)sourceService;
        _staffService = staffService;
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
        _pvnService = pvnService;
        _infoService = infoService;
        _pluginService = pluginService;
        _gameLaunchService = gameLaunchService;
    }

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            if (parameter is not GalgamePageParameter param)
            {
                _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
                return;
            }

            ShowBackgroundImage =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            ShowTagPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowTags)
                ? Visibility.Visible
                : Visibility.Collapsed;
            ShowCharacterPanel =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCharacters)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Item = param.Galgame;
            IsLocalGame = Item.IsLocalGame;
            _galgameService.GalgameMutated += OnGalgameMutated;
            _staffService.OnGameStaffChanged += Update;
            // 初始化面板
            Update(Item);

            Plugin = null;
            _pluginInfo = null;
            PluginUi = null;
            ObservableCollection<PluginX> plugins = await _pluginService.GetAllPluginsAsync();
            PluginX? p = plugins.FirstOrDefault(p => p is { IsLoaded: true, Plugin: IGalgamePage });
            Plugin = p?.Plugin as IGalgamePage;
            _pluginInfo = p?.Info;
            if (Plugin is not null)
                PluginUi = await PluginInvokeHelper.InvokeAsync(_pluginInfo!,
                    () => Plugin.CreateUiAsync(param.Galgame), _infoService);
            else
                await LoadPanelPluginUis(param.Galgame, plugins);

            if ((param.StartGame && await _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart))
                || param.ForceStartGame)
                await Play();
            if (param.SelectProgress)
            {
                await Task.Delay(1000);
                await SelectProcess();
            }

            TryUpdateGameInfo();
            return;

            // 尝试补充之前版本没有的信息
            void TryUpdateGameInfo()
            {
                if (Item is null) return;
                if (Item.HeaderImagePath.Value is null && !Item.AutoFetchStatus.HeaderImage)
                    _ = _galgameService.ParseGalInfoAsync(Item, type: GameParseType.HeaderImage);
                if (_staffService.GetStaffs(Item).Count == 0 && !Item.AutoFetchStatus.Staff)
                    _ = _staffService.ParseStaffAsync(Item);
            }
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Oops, something went wrong", e);
        }
    }

    public void OnNavigatedFrom()
    {
        _galgameService.GalgameMutated -= OnGalgameMutated;
        _staffService.OnGameStaffChanged -= Update;
    }

    private async Task LoadPanelPluginUis(Galgame game, IEnumerable<PluginX> plugins)
    {
        LeftPanelPluginUis.Clear();
        RightPanelPluginUis.Clear();
        foreach (PluginX p in plugins)
        {
            if (p is not { IsLoaded: true, Plugin: not null }) continue;
            try
            {
                if (p.Plugin is IGalgamePageLeftPanel leftPanel)
                {
                    UIElement? ui = await PluginInvokeHelper.InvokeAsync(p.Info,
                        () => leftPanel.CreateLeftPanelUiAsync(game), _infoService);
                    if (ui is not null) LeftPanelPluginUis.Add(ui);
                }
                if (p.Plugin is IGalgamePageRightPanel rightPanel)
                {
                    UIElement? ui = await PluginInvokeHelper.InvokeAsync(p.Info,
                        () => rightPanel.CreateRightPanelUiAsync(game), _infoService);
                    if (ui is not null) RightPanelPluginUis.Add(ui);
                }
            }
            catch (Exception ex)
            {
                _infoService.PluginEvent(p.Info, ex);
            }
        }
    }

    private void Update(Galgame? game)
    {
        if (game is null || game != Item) return;
        try
        {
            CanOpenInBgm = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Bangumi]);
            CanOpenInVndb = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Vndb]);
            CanOpenInYmgal = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Ymgal]);
            CanOpenInCngal = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Cngal]);
            CanOpenInHikarinagi = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Hikarinagi]);
            CanOpenInSteam = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Steam]);
            CanOpenInExternalWebsite = CanOpenInBgm || CanOpenInVndb || CanOpenInYmgal || CanOpenInCngal ||
                                       CanOpenInHikarinagi || CanOpenInSteam;
        }
        catch (Exception ex)
        {
            // 原理上来说是不会越界的，但莫名奇妙有用户反馈过越界问题
            _infoService.Info(InfoBarSeverity.Warning, $"Error setting open flags: {ex.Message}");
        }
        Installations.Clear();
        foreach (GalgameAndPath installation in Item?.LocalInstallations ?? [])
            Installations.Add(installation);
        LocalInstallationConfig? config = CurrentInstallationConfig;
        IsRemoveSelectedThreadVisible = config?.ProcessName is not null ? Visibility.Visible : Visibility.Collapsed;
        IsSelectProcessVisible = config?.ProcessName is null ? Visibility.Visible : Visibility.Collapsed;
        IsResetPathVisible = config?.ExePath is not null || config?.TextPath is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        HasSaveDirectory = !string.IsNullOrEmpty(config?.DetectedSavePath);

        // 根据是否有存档目录来设置打开按钮的显示
        if (HasSaveDirectory || Installations.Count > 1)
        {
            ShowSingleExplorerButton = Visibility.Collapsed;
            ShowExplorerMenu = Visibility.Visible;
        }
        else
        {
            ShowSingleExplorerButton = Visibility.Visible;
            ShowExplorerMenu = Visibility.Collapsed;
        }

        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(Installations));
        OnPropertyChanged(nameof(CurrentInstallationConfig));
        OnPropertyChanged(nameof(HasMultipleInstallations));
    }

    private void OnGalgameMutated(object? sender, GalgameMutationEventArgs args) => Update(args.Game);

    #region INFOBAR_CTRL

    private async Task DisplayMsg(InfoBarSeverity severity, string msg, int displayTimeMs = 3000)
    {
        _infoService.Info(severity, msg: msg, displayTimeMs: displayTimeMs);
        await Task.Delay(displayTimeMs); //保持兼容旧写法
    }

    #endregion

    [RelayCommand]
    private async Task OpenInBgm()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Bangumi])) return;
        await Launcher.LaunchUriAsync(new Uri("https://bgm.tv/subject/"+Item!.Ids[(int)RssType.Bangumi]));
    }

    [RelayCommand]
    private async Task OpenInVndb()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Vndb])) return;
        await Launcher.LaunchUriAsync(new Uri("https://vndb.org/v"+Item!.Ids[(int)RssType.Vndb]));
    }

    [RelayCommand]
    private async Task OpenInYmgal()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Ymgal])) return;
        await Launcher.LaunchUriAsync(new Uri("https://www.ymgal.games/ga"+Item!.Ids[(int)RssType.Ymgal]));
    }

    [RelayCommand]
    private async Task OpenInCngal()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Cngal])) return;
        await Launcher.LaunchUriAsync(new Uri("https://www.cngal.org/entries/index/"+Item!.Ids[(int)RssType.Cngal]));
    }

    [RelayCommand]
    private async Task OpenInHikarinagi()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Hikarinagi])) return;
        await Launcher.LaunchUriAsync(new Uri("https://www.hikarinagi.org/galgames/"+Item!.Ids[(int)RssType.Hikarinagi]));
    }

    [RelayCommand]
    private async Task OpenInSteam()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Steam])) return;
        await Launcher.LaunchUriAsync(new Uri("https://store.steampowered.com/app/"+Item!.Ids[(int)RssType.Steam]));
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task Play()
    {
        if (Item is null) return;
        List<GalgameAndPath> installations = Item.LocalInstallations.ToList();
        GalgameAndPath? installation = installations.FirstOrDefault(e => e.EntryId == Item.PreferredInstallationId);
        if (installation is null && installations.Count == 1)
            installation = installations[0];
        if (installation is null && installations.Count > 1)
            installation = await SelectInstallationAsync(installations);
        if (installation is null) return;
        await _gameLaunchService.LaunchAsync(Item, installation);
        Update(Item);
    }

    [RelayCommand]
    private async Task PlayInstallation(GalgameAndPath? installation)
    {
        if (Item is null || installation is null) return;
        await _gameLaunchService.LaunchAsync(Item, installation);
        Update(Item);
    }

    /// <summary>
    /// 将指定安装实例设为首选实例（不启动游戏），并持久化。
    /// </summary>
    [RelayCommand]
    private async Task SetPreferredInstallation(GalgameAndPath? installation)
    {
        if (Item is null || installation is null || !installation.IsLocalInstallation) return;
        if (Item.PreferredInstallationId == installation.EntryId) return;
        Item.SetPreferredInstallation(installation);
        if (installation.Source is not null) _sourceService.Save(installation.Source);
        await _galgameService.SaveGalgameAsync(Item);
        Update(Item);
    }

    private static async Task<GalgameAndPath?> SelectInstallationAsync(IReadOnlyList<GalgameAndPath> installations)
    {
        ComboBox selector = new()
        {
            ItemsSource = installations,
            DisplayMemberPath = nameof(GalgameAndPath.DisplayName),
            SelectedIndex = 0,
            MinWidth = 420,
        };
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "MultiInstall_SelectDialog_Title".GetLocalized(),
            Content = selector,
            PrimaryButtonText = "MultiInstall_Launch".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary
            ? selector.SelectedItem as GalgameAndPath
            : null;
    }

    [RelayCommand]
    private async Task GetInfoFromRss()
    {
        if (Item == null) return;
        IsPhrasing = true;
        try
        {
            await _galgameService.ParseGalInfoAsync(Item);
        }
        finally
        {
            IsPhrasing = false;
        }
    }

    [RelayCommand]
    private void Setting()
    {
        if (Item == null) return;
        _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, Item);
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task ChangeSavePosition()
    {
        if (Item?.PreferredLocalInstallation is not { } installation) return;
        try
        {
            await _galgameService.ChangeGalgameSavePosition(Item, installation);
            Update(Item);
        }
        catch (PvnException e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgamePage_ChangeSavePosFailed".GetLocalized(), e.Message);
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgamePage_ChangeSavePosFailed".GetLocalized(), e.ToString());
        }
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task ResetExePath()
    {
        if (Item?.PreferredLocalInstallation is not { } installation ||
            installation.LocalConfig is not { } config) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element
                ? element.RequestedTheme
                : ElementTheme.Default,
            Title = "GalgamePage_ResetExePath_Title".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        config.ExePath = null;
        if (installation.Source is not null)
            _sourceService.Save(installation.Source);
        Update(Item);
        _infoService.Info(InfoBarSeverity.Success, "GalgamePage_ResetExePath_Success".GetLocalized());
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task DeleteFromDisk()
    {
        if (Item is null || !Item.IsLocalGame) return;
        List<GalgameAndPath> deletable = Item.LocalInstallations
            .Where(e => e.Source?.SourceType == GalgameSourceType.LocalFolder).ToList();
        GalgameAndPath? installation = deletable.Count switch
        {
            0 => null,
            1 => deletable[0],
            _ => await SelectInstallationAsync(deletable),
        };
        if (installation is null)
        {
            _infoService.Info(InfoBarSeverity.Warning, "MultiInstall_DeleteFiles_LocalFolderOnly".GetLocalized());
            return;
        }
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "MultiInstall_DeleteFiles_Title".GetLocalized(),
            Content = "MultiInstall_DeleteFiles_Content".GetLocalized() + $"\n{installation.Path}",
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Secondary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            await _sourceService.MoveOutNoOperate(installation, true);
            IsLocalGame = Item.IsLocalGame;
            Update(Item);
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Success,
                "GalgamePage_Delete_Game_Success".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Error,
                "GalgamePage_Delete_Game_Error".GetLocalized() + e.Message, e);
        }
    }

    [RelayCommand]
    private async Task DeletePermanently()
    {
        if (Item is null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "MultiInstall_DeleteGame_Title".GetLocalized(),
            Content = "MultiInstall_DeleteGame_Content".GetLocalized() + $"\n{Item.Name.Value}",
            PrimaryButtonText = "MultiInstall_DeleteGame_Action".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await _galgameService.RemoveGalgame(Item);
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }

    [RelayCommand]
    private async Task OpenInExplorer()
    {
        await OpenInstallationInExplorer(Item?.PreferredLocalInstallation);
    }

    [RelayCommand]
    private async Task OpenInstallationInExplorer(GalgameAndPath? installation)
    {
        if (installation is null) return;
        if (!Directory.Exists(installation.Path))
        {
            _infoService.Info(InfoBarSeverity.Warning,
                "MultiInstall_PathUnavailable".GetLocalized(installation.Path));
            return;
        }
        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(installation.Path);
        await Launcher.LaunchFolderAsync(folder);
    }

    [RelayCommand]
    private async Task OpenSaveDirectory()
    {
        LocalInstallationConfig? config = Item?.PreferredLocalInstallation?.LocalConfig;
        if (config is null) return;
        if (string.IsNullOrWhiteSpace(config.DetectedSavePath?.ToPath()))
        {
            _infoService.Info(InfoBarSeverity.Warning, "GalgamePage_NoSaveDirectoryDetected".GetLocalized(), displayTimeMs: 3000);
            return;
        }

        try
        {
            var absolutePath = config.DetectedSavePath?.ToPath();
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                _infoService.Info(InfoBarSeverity.Error, "GalgamePage_OpenSaveDirectoryFailed".GetLocalized(), "GalgamePage_InvalidSavePath".GetLocalized());
                return;
            }
            await Launcher.LaunchUriAsync(new Uri(absolutePath));
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgamePage_OpenSaveDirectoryFailed".GetLocalized(), e.Message);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Item is null) return;
        // ReSharper disable once RedundantBoolCompare
        if (Item.EnableMagpie == true && string.IsNullOrEmpty(await _localSettingsService.ReadSettingAsync<string>(KeyValues.MagpiePath)))
        {
            Item.EnableMagpie = false;
            _infoService.Info(InfoBarSeverity.Warning, "CallMagpieTask_NoMagpiePath".GetLocalized());
        }
        if (Item.PreferredLocalInstallation?.Source is { } source)
            _sourceService.Save(source);
        await _galgameService.SaveGalgameAsync(Item);
        Update(Item);
    }

    [RelayCommand]
    private async Task ChangeRunInLocaleEmulator()
    {
        LocalInstallationConfig? config = CurrentInstallationConfig;
        if (config is null) return;
        if (config.RunInLocaleEmulator && !await CheckLocaleEmulator())
            config.RunInLocaleEmulator = false;

        if (!config.RunInLocaleEmulator)
        {
            config.ExeArguments = null;
            config.ExePath = null;
            await RemoveSelectedThread();
        }

        await CheckLocaleEmulator();
        await SaveAsync();
    }

    [RelayCommand]
    private async Task ChangeHighDpi()
    {
        LocalInstallationConfig? config = CurrentInstallationConfig;
        if (config is null || string.IsNullOrEmpty(config.ExePath))
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgamePage_HighDpi_ExePathIsEmpty".GetLocalized());
            if (config != null)
                config.HighDpi = false;
            return;
        }

        try
        {
            // 构建 PowerShell 命令
            var regPath = @"HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            var command = !config.HighDpi
                ? $"Remove-ItemProperty -Path '{regPath}' -Name '{config.ExePath.Replace("'", "''")}'"
                : $"Set-ItemProperty -Path '{regPath}' -Name '{config.ExePath.Replace("'", "''")}' -Value '~ PERPROCESSSYSTEMDPIFORCEOFF HIGHDPIAWARE'";

            // 创建启动管理员权限的 PowerShell 进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{command}\"",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                Process? process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        await SaveAsync();
                        _ = DisplayMsg(InfoBarSeverity.Success, "GalgamePage_HighDpi_Success".GetLocalized());
                    }
                    else
                    {
                        _infoService.Info(InfoBarSeverity.Error, "GalgamePage_HighDpi_Fail".GetLocalized() + $" {process.ExitCode}");
                    }
                }
            }
            catch (Win32Exception)
            {
                // 用户取消了UAC提示
                _infoService.Info(InfoBarSeverity.Warning, "GalgamePage_HighDpi_NeedAdmin".GetLocalized());
                config.HighDpi = !config.HighDpi;
            }
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgamePage_HighDpi_Fail".GetLocalized() + $" {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ChangePlayStatus()
    {
        //Idea: 加一个检测是否有对应源的ID
        if (Item == null) return;
        ChangePlayStatusDialog dialog = new(Item)
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
        };
        await dialog.ShowAsync();
        if (dialog.Canceled) return;
        if (dialog.UploadToBgm)
        {
            _ = DisplayMsg(InfoBarSeverity.Informational, "HomePage_UploadingToBgm".GetLocalized(), 1000 * 10);
            (GalStatusSyncResult, string) result = await _galgameService.UploadPlayStatusAsync(Item, RssType.Bangumi);
            await DisplayMsg(result.Item1.ToInfoBarSeverity(), result.Item2);
        }

        if (dialog.UploadToVndb)
        {
            _ = DisplayMsg(InfoBarSeverity.Informational, "HomePage_UploadingToVndb".GetLocalized(), 1000 * 10);
            (GalStatusSyncResult, string) result = await _galgameService.UploadPlayStatusAsync(Item, RssType.Vndb);
            await DisplayMsg(result.Item1.ToInfoBarSeverity(), result.Item2);
        }
        await _galgameService.SaveGalgameAsync(Item);
        _pvnService.Upload(Item, PvnUploadProperties.Review);
    }

    [RelayCommand]
    private async Task SyncFromBgm()
    {
        if (Item == null) return;
        _ =  DisplayMsg(InfoBarSeverity.Informational, "HomePage_Downloading".GetLocalized(), 1000 * 100);
        (GalStatusSyncResult, string) result = await _galgameService.DownLoadPlayStatusAsync(Item, RssType.Bangumi);
        await DisplayMsg(result.Item1.ToInfoBarSeverity(), result.Item2);
    }

    [RelayCommand]
    private async Task SyncFromVndb()
    {
        if (Item == null) return;
        _ =  DisplayMsg(InfoBarSeverity.Informational, "HomePage_Downloading".GetLocalized(), 1000 * 100);
        (GalStatusSyncResult, string) result = await _galgameService.DownLoadPlayStatusAsync(Item, RssType.Vndb);
        await DisplayMsg(result.Item1.ToInfoBarSeverity(), result.Item2);
    }

    [RelayCommand(CanExecute = nameof(IsNotLocalGame))]
    private async Task SetLocalPath()
    {
        try
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            openPicker.FileTypeFilter.Add(".bat");
            openPicker.FileTypeFilter.Add(".EXE");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is not null)
            {
                var folder = file.Path[..file.Path.LastIndexOf('\\')];
                await _galgameService.SetLocalPathAsync(Item!, folder);
                if (Item!.PreferredLocalInstallation?.LocalConfig is { } config)
                {
                    config.ExePath = file.Path;
                    if (Item.PreferredLocalInstallation.Source is not null)
                        _sourceService.Save(Item.PreferredLocalInstallation.Source);
                }
                IsLocalGame = Item!.IsLocalGame;
                Update(Item);
                _ = DisplayMsg(InfoBarSeverity.Success, "GalgamePage_PathSet".GetLocalized());
                _galgameService.RefreshDisplay(); //重新构造显示列表以刷新特殊显示非本地游戏（因为GameToOpacityConverter只会在构造列表的时候被调用）
            }
        }
        catch (Exception e)
        {
            _ = DisplayMsg(InfoBarSeverity.Error, e.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task RemoveSelectedThread()
    {
        if (CurrentInstallationConfig is not { } config) return;
        config.ProcessName = null;
        Update(Item);
        _ = DisplayMsg(InfoBarSeverity.Success, "GalgamePage_RemoveSelectedThread_Success".GetLocalized());
        await SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task SelectProcess()
    {
        if (CurrentInstallationConfig is not { } config) return;
        SelectProcessDialog dialog = new();
        await dialog.ShowAsync();
        if (dialog.SelectedProcessName is not null)
        {
            config.ProcessName = dialog.SelectedProcessName;
            Update(Item);
            await SaveAsync();
            _ = DisplayMsg(InfoBarSeverity.Success, "HomePage_ProcessNameSet".GetLocalized());
        }
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task SelectText()
    {
        GalgameAndPath? installation = Item?.PreferredLocalInstallation;
        LocalInstallationConfig? config = installation?.LocalConfig;
        if (Item is null || installation is null || config is null) return;
        var path = config.TextPath;
        if (path is null || File.Exists(path) == false)
        {
            List<string>? customExtensions =
                await _localSettingsService.ReadSettingAsync<List<string>>(KeyValues.CustomTextFileExtensions);
            if (customExtensions is null || customExtensions.Count == 0)
            {
                // Fallback to a basic default list if settings are somehow empty/corrupt,
                // though LocalSettingsService.TryGetDefaultValue should prevent nulls.
                customExtensions = [".txt", ".pdf", ".md", ".doc", ".docx"];
            }
            SelectFileDialog dialog = new(installation.Path, customExtensions,
                "GalgamePage_SelectText_Title".GetLocalized());
            await dialog.ShowAsync();
            path = dialog.SelectedFilePath;
            if (dialog.RememberMe)
            {
                config.TextPath = path;
                await SaveAsync();
            }
        }

        if (path is not null)
            _ = Launcher.LaunchUriAsync(new Uri(path));
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task ClearText()
    {
        if (CurrentInstallationConfig is not { } config) return;
        config.TextPath = null;
        await SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task MoveToSource()
    {
        if (Item is null) return;
        ChangeSourceDialog dialog = new(Item);
        await dialog.ShowAsync();
        if (!dialog.Ok) return;
        try
        {
            _sourceService.MoveAsync(dialog.MoveInSource, dialog.MoveInSource.Path,
                dialog.MoveOutSource, Item, dialog.DeleteFiles);
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.Message);
        }
    }

    private async Task<bool> CheckLocaleEmulator()
    {
        var path = await _localSettingsService.ReadSettingAsync<string>(KeyValues.LocaleEmulatorPath);
        if (path is not null && File.Exists(path)) return true;
        _infoService.Info(InfoBarSeverity.Warning, msg: "GalgamePage_InvalidLocaleEmulatorPath".GetLocalized(),
            displayTimeMs: 5000);
        return false;
    }

    [RelayCommand]
    private async Task ResetPath()
    {
        LocalInstallationConfig? config = CurrentInstallationConfig;
        if (Item is null || config is null) return;
        if (config.HighDpi)
            await ChangeHighDpi();
        if (config.HighDpi)
            config.HighDpi = false;
        config.ExePath = null;
        await ClearText();

    }

    // 管理游戏详情页布局
    [RelayCommand]
    private async Task ManageLayout()
    {
        if (Plugin is IGalgamePageSetting setting)
        {
            await PluginInvokeHelper.InvokeAsync(_pluginInfo!, setting.SettingAsync, _infoService);
            return;
        }
        ManageGalgamePageLayoutDialog dialog = new();
        _ = dialog.ShowAsync();
    }

    #region Character Management

    [RelayCommand]
    private async Task AddCharacter(GalgameCharacter? character)
    {
        if (Item == null) return;

        var newCharacter = new GalgameCharacter
        {
            Name = "GameCharacterPanel_NewCharacter".GetLocalized(),
            PreviewImagePath = Galgame.DefaultCharacterImagePath,
            ImagePath = Galgame.DefaultCharacterImagePath
        };

        var insertIndex = character != null ? Item.Characters.IndexOf(character) + 1 : Item.Characters.Count;
        Item.Characters.Insert(insertIndex, newCharacter);

        await SaveAsync();
        _infoService.Info(InfoBarSeverity.Success, "GameCharacterPanel_AddCharacter_Success".GetLocalized());
    }

    [RelayCommand]
    private async Task DeleteCharacter(GalgameCharacter? character)
    {
        if (Item == null || character == null) return;

        Item.Characters.Remove(character);
        await SaveAsync();
        _infoService.Info(InfoBarSeverity.Success, "GameCharacterPanel_DeleteCharacter_Success".GetLocalized());
    }

    #endregion
}

public class GalgamePageParameter
{
    /// 目标游戏
    [Required] public Galgame Galgame = null!;
    /// 如果设置有打开直接启动游戏，则直接启动游戏
    public bool StartGame;
    /// 强制启动游戏
    public bool ForceStartGame;
    /// 显示手动选择线程弹窗
    public bool SelectProgress;
}
