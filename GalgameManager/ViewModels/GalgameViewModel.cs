using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class GalgameViewModel : ObservableRecipient, INavigationAware
{
    private const int ProcessMaxWaitSec = 60; //(手动指定游戏进程)等待游戏进程启动的最大时间
    private const int ManuallySelectProcessSec = 15; //认定为需要手动选择游戏进程的时间阈值
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly JumpListService _jumpListService;
    [ObservableProperty] private Galgame? _item;
    [ObservableProperty] private bool _isLocalGame; //是否是本地游戏（而非云端同步过来/本地已删除的虚拟游戏）
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Visibility _isTagVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isDescriptionVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isRemoveSelectedThreadVisible = Visibility.Collapsed;
    [ObservableProperty] private bool _canOpenInBgm;
    [ObservableProperty] private bool _canOpenInVndb;

    [ObservableProperty] private bool _infoBarOpen;
    [ObservableProperty] private string _infoBarMsg = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    private int _msgIndex;

    public GalgameViewModel(IDataCollectionService<Galgame> dataCollectionService, INavigationService navigationService, IJumpListService jumpListService, ILocalSettingsService localSettingsService)
    {
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _navigationService = navigationService;
        _galgameService.PhrasedEvent += OnGalgameServiceOnPhrasedEvent;
        _jumpListService = (JumpListService)jumpListService;
        _localSettingsService = localSettingsService;
    }

    

    public async void OnNavigatedTo(object parameter)
    {
        var path = parameter as string ?? null;
        var startGame = false;
        Tuple<string, bool>? para = parameter as Tuple<string, bool> ?? null;
        if (para != null)
        {
            path = para.Item1;
            startGame = para.Item2;
        }

        ObservableCollection<Galgame>? data = await _dataCollectionService.GetContentGridDataAsync();
        Item = parameter as Galgame ?? data.FirstOrDefault(g => g.Path == path);
        if (Item is null) //找不到这个游戏，回到主界面
        {
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
            return;
        }

        IsLocalGame = Item.CheckExist();
        Item.SavePath = Item.SavePath; //更新存档位置显示
        UpdateVisibility();
        if (startGame && await _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart))
            await Play();
    }

    public void OnNavigatedFrom()
    {
        _galgameService.PhrasedEvent -= OnGalgameServiceOnPhrasedEvent;
    }
    
    private void UpdateVisibility()
    {
        IsTagVisible = Item?.Tags.Value?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        IsDescriptionVisible = Item?.Description! != string.Empty ? Visibility.Visible : Visibility.Collapsed;
        CanOpenInBgm = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Bangumi]);
        CanOpenInVndb = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Vndb]);
        IsRemoveSelectedThreadVisible = Item?.ProcessName is not null ? Visibility.Visible : Visibility.Collapsed;
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
    
    private void OnGalgameServiceOnPhrasedEvent() => IsPhrasing = false;

    #region INFOBAR_CTRL

    private async Task DisplayMsg(InfoBarSeverity severity, string msg, int displayTimeMs = 3000)
    {
        var myIndex = ++_msgIndex;
        InfoBarOpen = true;
        InfoBarMsg = msg;
        InfoBarSeverity = severity;
        await Task.Delay(displayTimeMs);
        if (myIndex == _msgIndex)
            InfoBarOpen = false;
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
    private async Task Play()
    {
        if (Item == null) return;
        if (Item.ExePath == null)
            await _galgameService.GetGalgameExeAsync(Item);
        if (Item.ExePath == null) return;

        Item.LastPlay = DateTime.Now.ToShortDateString();
        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Item.ExePath,
                WorkingDirectory = Item.Path,
                UseShellExecute = Item.RunAsAdmin,
                Verb = Item.RunAsAdmin ? "runas" : null
            }
        };
        try
        {
            DateTime startTime = DateTime.Now;
            process.Start();
            _galgameService.Sort();
            if (Item.ProcessName is not null)
            {
                await Task.Delay(1000 * 2); //有可能引导进程和游戏进程是一个名字，等2s让引导进程先退出
                process = await WaitForProcessStartAsync(Item.ProcessName) ?? process;
            }

            await Task.Delay(1000); //等待1000ms，让游戏进程启动后再最小化
            Process.GetProcessesByName(string.Empty);
            Item.RecordPlayTime(process);
            ((OverlappedPresenter)App.MainWindow.AppWindow.Presenter).Minimize(); //最小化窗口
            await _jumpListService.AddToJumpListAsync(Item);
            Dictionary<string, int> oldPlayTime = new(Item.PlayedTime);

            await process.WaitForExitAsync();

            ((OverlappedPresenter)App.MainWindow.AppWindow.Presenter).Restore(); //恢复窗口
            if (DateTime.Now - startTime < TimeSpan.FromSeconds(ManuallySelectProcessSec) && Item.ProcessName is null)
            {
                SelectProcessDialog dialog = new();
                await dialog.ShowAsync();
                if (dialog.SelectedProcessName is not null)
                {
                    Item.ProcessName = dialog.SelectedProcessName;
                    UpdateVisibility();
                    await DisplayMsg(InfoBarSeverity.Success, "HomePage_ProcessNameSet".GetLocalized());
                }
            }
            await SaveAsync(); //保存游戏信息(更新时长)
            foreach(var date in Item.PlayedTime.Keys)
            {
                if (oldPlayTime.TryGetValue(date, out var oldValue) && oldValue == Item.PlayedTime[date]) continue;
                await _galgameService.CommitChange(CommitType.Play, Item, (date, Item.PlayedTime[date] - oldValue));
            }
        }
        catch
        {
            //ignore : 用户取消了UAC
        }
    }

    [RelayCommand]
    private async Task GetInfoFromRss()
    {
        if (Item == null) return;
        IsPhrasing = true;
        await _galgameService.PhraseGalInfoAsync(Item);
        UpdateVisibility();
    }

    [RelayCommand]
    private void Setting()
    {
        if (Item == null) return;
        _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, Item);
    }

    [RelayCommand]
    private async Task ChangeSavePosition()
    {
        if(Item == null) return;
        await _galgameService.ChangeGalgameSavePosition(Item);
    }
    
    [RelayCommand]
    private void ResetExePath(object obj)
    {
        Item!.ExePath = null;
    }
    
    [RelayCommand]
    private async Task DeleteFromDisk()
    {
        if(Item == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "HomePage_Delete_Title".GetLocalized(),
            Content = "HomePage_Delete_Message".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Secondary
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(Item, true, true);
        };
        await dialog.ShowAsync();
    }
    
    [RelayCommand]
    private async Task OpenInExplorer()
    {
        if(Item == null) return;
        await Launcher.LaunchUriAsync(new Uri(Item.Path));
    }

    [RelayCommand]
    private void JumpToPlayedTimePage()
    {
        _navigationService.NavigateTo(typeof(PlayedTimeViewModel).FullName!, Item);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _galgameService.SaveGalgamesAsync(Item);
    }

    [RelayCommand]
    private async Task ChangePlayStatus()
    {
        if (Item == null) return;
        ChangePlayStatusDialog dialog = new(Item)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
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
            throw new NotImplementedException();
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
    private async Task SetLocalPath()
    {
        try
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            openPicker.FileTypeFilter.Add(".bat");
            openPicker.FileTypeFilter.Add(".EXE");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is not null)
            {
                var folder = file.Path[..file.Path.LastIndexOf('\\')];
                if (_galgameService.GetGalgameFromPath(folder) is not null)
                {
                    _ = DisplayMsg(InfoBarSeverity.Error, "GalgamePage_PathAlreadyExist".GetLocalized());
                    return;
                }
                await _galgameService.TryAddGalgameAsync(folder);
                Item!.ExePath = file.Path;
                IsLocalGame = Item!.CheckExist();
                _ = DisplayMsg(InfoBarSeverity.Success, "GalgamePage_PathSet".GetLocalized());
                _galgameService.RefreshDisplay(); //重新构造显示列表以刷新特殊显示非本地游戏（因为GameToOpacityConverter只会在构造列表的时候被调用）
            }
        }
        catch (Exception e)
        {
            _ = DisplayMsg(InfoBarSeverity.Error, e.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedThread()
    {
        Item!.ProcessName = null;
        UpdateVisibility();
        _ = DisplayMsg(InfoBarSeverity.Success, "GalgamePage_RemoveSelectedThread_Success".GetLocalized());
        await SaveAsync();
    }
}