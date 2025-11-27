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
using System.Globalization;
using GalgameManager.Views.GalgamePagePanel;
using ValveKeyValue;

namespace GalgameManager.ViewModels;

public partial class GalgameViewModel : ObservableObject, INavigationAware
{
    private const int ProcessMaxWaitSec = 60; //(手动指定游戏进程)等待游戏进程启动的最大时间
    private readonly GalgameCollectionService _galgameService;
    private readonly GalgameSourceCollectionService _sourceService;
    private readonly IStaffService _staffService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly JumpListService _jumpListService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IPvnService _pvnService;
    private readonly IInfoService _infoService;
    [ObservableProperty] private Galgame? _item;
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
    [ObservableProperty] private bool _canOpenInSteam;
    [ObservableProperty] private Visibility _showBackgroundImage = Visibility.Collapsed;
    [ObservableProperty] private Visibility _showTagPanel = Visibility.Collapsed;
    [ObservableProperty] private Visibility _showCharacterPanel = Visibility.Collapsed;
    private bool IsNotLocalGame => !IsLocalGame;

    [ObservableProperty]
    private bool _useNewLayout;

    public GalgameViewModel(IGalgameCollectionService dataCollectionService, IStaffService staffService,
        INavigationService navigationService, IJumpListService jumpListService,
        ILocalSettingsService localSettingsService, IBgTaskService bgTaskService,
        IPvnService pvnService, IInfoService infoService, IGalgameSourceCollectionService sourceService)
    {
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _sourceService = (GalgameSourceCollectionService)sourceService;
        _staffService = staffService;
        _navigationService = navigationService;
        _jumpListService = (JumpListService)jumpListService;
        _localSettingsService = localSettingsService;
        _bgTaskService = bgTaskService;
        _pvnService = pvnService;
        _infoService = infoService;
        
        // 订阅布局更改事件
        ManageGalgamePageLayoutDialog.LayoutChanged += OnLayoutChanged;
    }
    
    // 布局更改时更新视图
    private async void OnLayoutChanged(object? sender, bool newLayoutValue)
    {
        try
        {
            UseNewLayout = newLayoutValue;
            ShowBackgroundImage = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            ShowTagPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowTags) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            ShowCharacterPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCharacters) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            Update(Item);
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
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
            
            UseNewLayout = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout);
            ShowBackgroundImage = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            ShowTagPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowTags) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            ShowCharacterPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCharacters) 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            Item = param.Galgame;
            IsLocalGame = Item.IsLocalGame;
            Item.SavePath = Item.SavePath;
            _galgameService.PhrasedEvent2 += Update;
            _staffService.OnGameStaffChanged += Update;
            // 初始化面板
            Update(Item);
        
            if (param.StartGame && await _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart))
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
        _galgameService.PhrasedEvent2 -= Update;
        _staffService.OnGameStaffChanged -= Update;
        ManageGalgamePageLayoutDialog.LayoutChanged -= OnLayoutChanged;
    }
    
    /// <summary>
    /// 等待游戏进程启动，若超时则返回null
    /// </summary>
    /// <param name="processName">进程名</param>
    private static async Task<Process?> WaitForProcessStartAsync(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        var waitSec = 0;
        while (processes.Length == 0)
        {
            await Task.Delay(100);
            processes = Process.GetProcessesByName(processName);
            if (++waitSec > ProcessMaxWaitSec)
                return null;
        }
        return processes[0];
    }
    
    private void Update(Galgame? game)
    {
        if (game is null || game != Item) return;
        IsPhrasing = false;
        try
        {
            CanOpenInBgm = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Bangumi]);
            CanOpenInVndb = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Vndb]);
            CanOpenInYmgal = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Ymgal]);
            CanOpenInCngal = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Cngal]);
            CanOpenInSteam = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Steam]);
        }
        catch (Exception ex)
        {
            // 原理上来说是不会越界的，但莫名奇妙有用户反馈过越界问题
            _infoService.Info(InfoBarSeverity.Warning, $"Error setting open flags: {ex.Message}");
        }
        IsRemoveSelectedThreadVisible = Item?.ProcessName is not null ? Visibility.Visible : Visibility.Collapsed;
        IsSelectProcessVisible = Item?.ProcessName is null ? Visibility.Visible : Visibility.Collapsed;
        IsResetPathVisible = Item?.ExePath is not null || Item?.TextPath is not null ? Visibility.Visible : Visibility.Collapsed;
        HasSaveDirectory = !string.IsNullOrEmpty(Item?.DetectedSavePosition);

        // 根据是否有存档目录来设置打开按钮的显示
        if (HasSaveDirectory)
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
    }

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
    private async Task OpenInSteam()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Steam])) return;
        await Launcher.LaunchUriAsync(new Uri("https://store.steampowered.com/app/"+Item!.Ids[(int)RssType.Steam]));
    }
    
    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task Play()
    {
        if (!Item!.IsLocalGame) return;
        if (Item.ExePath is not null && !File.Exists(Item.ExePath)) Item.ExePath = null;

        if (Item.Sources.Any(s => s.SourceType is GalgameSourceType.Steam) && Item.GetId(RssType.Steam) == -1)
        {
            Item.Ids[(int)RssType.Steam] = await TryGetSteamIdAsync();
            if (string.IsNullOrEmpty(Item.Ids[(int)RssType.Steam]))
                _infoService.Info(InfoBarSeverity.Warning, msg:"GalgamePage_Play_NoSteamId".GetLocalized());
        }
        
        var isSteamGame = Item.Sources.Any(s => s.SourceType is GalgameSourceType.Steam) &&
                          !string.IsNullOrEmpty(Item.Ids[(int)RssType.Steam]);
        if (string.IsNullOrEmpty(Item.ExePath) && !isSteamGame)
        {
            await _galgameService.GetGalgameExeAsync(Item);
            if (string.IsNullOrEmpty(Item.ExePath)) return;
        }

        Process process = null!;
        // 非steam游戏启动参数
        if (!isSteamGame)
        {
            var exePath = Item.ExePath;
            var args = Item.ExeArguments;
            if (Item.RunInLocaleEmulator && await CheckLocaleEmulator())
            {
                exePath = await _localSettingsService.ReadSettingAsync<string>(KeyValues.LocaleEmulatorPath);
                args = Item.ExePath;
            }

            ProcessStartInfo info = new()
            {
                FileName = exePath,
                CreateNoWindow = !string.IsNullOrEmpty(args),
                WorkingDirectory = Item.LocalPath,
                UseShellExecute = Item.RunAsAdmin | Item.ExePath!.ToLower().EndsWith("lnk"),
                Verb = Item.RunAsAdmin ? "runas" : null,
            };
            if (args is not null) info.ArgumentList.Add(args);
            process = new() { StartInfo = info };
        }
        // Steam游戏第一次启动会弹窗警告，提示用户选择游戏进程以记录游戏时长
        else if (isSteamGame && string.IsNullOrEmpty(Item.ProcessName) && !await DisplaySteamMsgAsync()) return; //false:取消对话框

        try
        {
            if (!isSteamGame)
                process.Start();
            else
            {
                Uri steamUri = new($"steam://run/{Item.Ids[(int)RssType.Steam]}");
                _infoService.Info(InfoBarSeverity.Informational, msg: "GalgamePage_Play_StartingSteam".GetLocalized());
                if (await Launcher.LaunchUriAsync(steamUri) == false)
                {
                    _infoService.Info(InfoBarSeverity.Error, "GalgamePage_Play_SteamLaunchError".GetLocalized());
                    return;
                }
                if (string.IsNullOrEmpty(Item.ProcessName))
                {
                    await SelectProcess();
                    return;
                }
            }

            Item.LastPlayTime = DateTime.Now;
            await _galgameService.SaveGalgameAsync(Item);
            // _galgameService.Sort();
            if (Item.ProcessName is not null)
            {
                await Task.Delay(1000 * 2); //有可能引导进程和游戏进程是一个名字，等2s让引导进程先退出
                process = await WaitForProcessStartAsync(Item.ProcessName) ?? process;
            }
            if (!string.IsNullOrEmpty(Item.ExeArguments) && Item.ProcessName is null) 
            { 
                //启动的进程和游戏进程不是同一个进程，需要知道到底启动什么进程
                await Task.Delay(1000 * 2);
                if (TryGetProcessFromName() is { } p) // 尝试根据游戏可执行文件名获取进程
                {
                    process = p;
                    Item.ProcessName = p.ProcessName;
                }
                else
                    await SelectProcess();
            }
            await _galgameService.SaveGalgameAsync(Item);
            _ = _bgTaskService.AddBgTask(new RecordPlayTimeTask(Item, process));
            await _jumpListService.AddToJumpListAsync(Item);
            
            await Task.Delay(1000); //等待1000ms，让游戏进程启动后再最小化
            if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.AlwaysEnableMagpie) || Item.EnableMagpie) 
                _ = _bgTaskService.AddBgTask(new CallMagpieTask(Item, process));
            if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.AlwaysMuteInBackground) || Item.MuteInBackground)
                _ = _bgTaskService.AddBgTask(new GameMuteTask(Item, process));
            if ((await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GameReMapEnabled) || Item.KeyReMap) && Item.KeyMappings.Any(m => m.IsEnabled))
                _ = _bgTaskService.AddBgTask(new KeyMappingTask(Item, process));            
            if ((await _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoDetectSavePath)) && string.IsNullOrEmpty(Item.DetectedSavePosition))
                _ = _bgTaskService.AddBgTask(new GameSaveDetectorTask(Item));            
            if (process.HasExited == false)
                App.SetWindowMode(await _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode));
            
            await process.WaitForExitAsync();
        }

        catch (Win32Exception e)
        {
            // 可能是用户取消了UAC提示
            if (e.NativeErrorCode == 1223) 
            {
                _infoService.Info(InfoBarSeverity.Warning, "GalgamePage_Play_CancelledByUser".GetLocalized());
                return;
            }
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Error, "GalgamePage_Play_Error".GetLocalized() + e.Message);
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Error, "GalgamePage_Play_Error".GetLocalized() + e.Message);
        }
        return;

        // 尝试获取Steam ID
        async Task<string?> TryGetSteamIdAsync()
        {
            _infoService.Info(InfoBarSeverity.Informational, msg:"GalgamePage_Play_GettingSteamId".GetLocalized());
            try
            {
                if (Item is null) return null;
                var path = Item.Sources.FirstOrDefault(s => s.SourceType == GalgameSourceType.Steam)?.GetPath(Item);
                if (path is null || !Directory.Exists(path)) return null;
                DirectoryInfo? di = new(path);
                di = di.Parent?.Parent;
                if (di is null) throw new PvnException("Cannot find steamapps folder");
                foreach (FileInfo file in di.GetFiles("appmanifest_*.acf"))
                {
                    await using FileStream fs = file.OpenRead();
                    KVValue? kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(fs).Value;
                    if (kv is null) continue;
                    var name = kv["name"].ToString(CultureInfo.InvariantCulture);
                    if (path.Contains(name)) return kv["appid"].ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception e)
            {
                _infoService.DeveloperEvent(e: e);
            }
            finally
            {
                _infoService.Info(InfoBarSeverity.Informational);
            }
            return null;
        }

        // 使用steam启动游戏，第一次弹窗警告需要手动选择游戏进程以记录游戏时长，若返回bool则是取消了对话框
        async Task<bool> DisplaySteamMsgAsync()
        {
            if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.NotifiedSteamNeedManual)) return true;
            BasicDialog dialog = new("GalgamePage_Play_SteamDialog_Title".GetLocalized(),
                "GalgamePage_Play_SteamDialog_Message".GetLocalized(),
                checkBoxText:"GalgamePage_Play_SteamDialog_CheckBox".GetLocalized());
            await dialog.ShowAsync();
            if (dialog.PrimaryButtonClicked)
            {
                await _localSettingsService.SaveSettingAsync(KeyValues.NotifiedSteamNeedManual, dialog.CheckBoxChecked);
                return true;
            }
            return false;
        }
    }

    [RelayCommand]
    private async Task GetInfoFromRss()
    {
        if (Item == null) return;
        IsPhrasing = true;
        await _galgameService.ParseGalInfoAsync(Item);
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
        if (Item?.IsLocalGame != true) return;
        try
        {
            await _galgameService.ChangeGalgameSavePosition(Item);
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
    private void ResetExePath(object obj)
    {
        if (Item is null || !Item.IsLocalGame) return;
        Item!.ExePath = null;
    }
    
    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task DeleteFromDisk()
    {
        if (Item is null || !Item.IsLocalGame) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "HomePage_Delete_Title".GetLocalized(),
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "HomePage_Delete_Message".GetLocalized(), Margin = new Thickness(0, 0, 0, 30) },
                    new CheckBox { Content = "HomePage_Delete_FromLibrary".GetLocalized(), IsChecked = true }
                }
            },
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Secondary
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            CheckBox? checkBox = (CheckBox)((StackPanel)dialog.Content).Children[1];
            var deleteFromLibrary = checkBox.IsChecked ?? false;
            var path = Item.Sources.FirstOrDefault(s => s.SourceType == GalgameSourceType.LocalFolder)?.GetPath(Item);
            if (path is not null)
            {
                try
                {
                    StorageFolder? folder = await StorageFolder.GetFolderFromPathAsync(path);
                    await folder.DeleteAsync(StorageDeleteOption.Default);
                }
                catch (Exception e)
                {
                    App.GetService<IInfoService>().Event(EventType.GalgameEvent, InfoBarSeverity.Error, "GalgamePage_Delete_Game_Error".GetLocalized() + e.Message);
                }
            }
            if (deleteFromLibrary)
            {
                await _galgameService.RemoveGalgame(Item, true);
            }
            else
            {
                GalgameSourceBase? source = _sourceService.GetGalgameSources().FirstOrDefault(s => s.Galgames.Any(g => g.Galgame == Item));
                if (source != null)
                {
                    _sourceService.MoveOutNoOperate(source, Item);
                }
                else
                {
                    App.GetService<IInfoService>().Event(EventType.GalgameEvent, InfoBarSeverity.Warning, "GalgamePage_Delete_Game_Error".GetLocalized());
                }
            }

            App.GetService<IInfoService>().Event(EventType.GalgameEvent, InfoBarSeverity.Success, "GalgamePage_Delete_Game_Success".GetLocalized());
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
        };
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task OpenInExplorer()
    {
        if(Item == null) return;
        var path = Item.Sources
            .FirstOrDefault(s => s.SourceType is GalgameSourceType.LocalFolder or GalgameSourceType.Steam)
            ?.GetPath(Item);
        if (path is null) //不应该发生
        {
            _infoService.DeveloperEvent(InfoBarSeverity.Error, "Can't find the path of the game");
            return;
        }
        await Launcher.LaunchUriAsync(new Uri(path));
    }

    [RelayCommand]
    private async Task OpenSaveDirectory()
    {
        if (Item == null) return;
        if (string.IsNullOrWhiteSpace(Item.DetectedSavePosition))
        {
            _infoService.Info(InfoBarSeverity.Warning, "GalgamePage_NoSaveDirectoryDetected".GetLocalized(), displayTimeMs: 3000);
            return;
        }

        try
        {
            var absolutePath = SaveDetectionConstants.GetAbsolutePath(Item.DetectedSavePosition, Item.LocalPath);
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
        await _galgameService.SaveGalgameAsync(Item);
    }

    [RelayCommand]
    private async Task ChangeRunInLocaleEmulator()
    {
        if (Item is null) return;
        if (Item.RunInLocaleEmulator && !await CheckLocaleEmulator())
            Item.RunInLocaleEmulator = false;

        if (!Item.RunInLocaleEmulator)
        {
            Item.ExeArguments = null;
            Item.ExePath = null;
            await RemoveSelectedThread();
        }

        await CheckLocaleEmulator();
        await SaveAsync();
    }

    [RelayCommand]
    private async Task ChangeHighDpi()
    {
        if (Item is null || string.IsNullOrEmpty(Item.ExePath)) 
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgamePage_HighDpi_ExePathIsEmpty".GetLocalized());
            if (Item != null)
                Item.HighDpi = false;
            return;
        }
        
        try 
        {
            // 构建 PowerShell 命令
            var regPath = @"HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            var command = !Item.HighDpi
                ? $"Remove-ItemProperty -Path '{regPath}' -Name '{Item.ExePath.Replace("'", "''")}'"
                : $"Set-ItemProperty -Path '{regPath}' -Name '{Item.ExePath.Replace("'", "''")}' -Value '~ PERPROCESSSYSTEMDPIFORCEOFF HIGHDPIAWARE'";

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
                Item.HighDpi = !Item.HighDpi;
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
                Item!.ExePath = file.Path;
                IsLocalGame = Item!.IsLocalGame;
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
        Item!.ProcessName = null;
        Update(Item);
        _ = DisplayMsg(InfoBarSeverity.Success, "GalgamePage_RemoveSelectedThread_Success".GetLocalized());
        await SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task SelectProcess()
    {
        if (!Item!.IsLocalGame) return;
        SelectProcessDialog dialog = new();
        await dialog.ShowAsync();
        if (dialog.SelectedProcessName is not null)
        {
            Item.ProcessName = dialog.SelectedProcessName;
            Update(Item);
            await SaveAsync();
            _ = DisplayMsg(InfoBarSeverity.Success, "HomePage_ProcessNameSet".GetLocalized());
        }
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task SelectText()
    {
        if (Item is null || !Item.IsLocalGame) return;
        var path = Item.TextPath;
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
            SelectFileDialog dialog = new(Item!.LocalPath!, customExtensions,
                "GalgamePage_SelectText_Title".GetLocalized());
            await dialog.ShowAsync();
            path = dialog.SelectedFilePath;
            if (dialog.RememberMe)
            {
                Item.TextPath = path;
                await SaveAsync();
            }
        }
        
        if (path is not null)
            _ = Launcher.LaunchUriAsync(new Uri(path));
    }
    
    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task ClearText()
    {
        if (Item is null) return;
        Item.TextPath = null;
        await SaveAsync();
    }

    [RelayCommand]
    private async Task MoveToSource()
    {
        if (Item is null) return;
        ChangeSourceDialog dialog = new(Item);
        await dialog.ShowAsync();
    }

    private async Task<bool> CheckLocaleEmulator()
    {
        var path = await _localSettingsService.ReadSettingAsync<string>(KeyValues.LocaleEmulatorPath);
        if (path is not null && File.Exists(path)) return true;
        _infoService.Info(InfoBarSeverity.Warning, msg: "GalgamePage_InvalidLocaleEmulatorPath".GetLocalized(),
            displayTimeMs: 5000);
        return false;
    }

    private Process? TryGetProcessFromName()
    {
        if (Item?.ExePath is null) return null;
        var name = Path.GetFileNameWithoutExtension(Item.ExePath);
        return Process.GetProcesses().FirstOrDefault(p => p.ProcessName == name);
    }

    [RelayCommand]
    private async Task ResetPath()
    {
        if (Item is null || !Item.IsLocalGame) return;
        if (Item.HighDpi)
            await ChangeHighDpi();
        if (Item.HighDpi)
            Item.HighDpi = false;
        Item!.ExePath = null;
        await ClearText();
        
    }

    // 管理游戏详情页布局
    [RelayCommand]
    private void ManageLayout()
    {
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
    /// 显示手动选择线程弹窗
    public bool SelectProgress;
}
