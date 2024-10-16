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
using GalgameManager.Models.Filters;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.RegularExpressions;

namespace GalgameManager.ViewModels;

public partial class GalgameViewModel : ObservableObject, INavigationAware
{
    private const int ProcessMaxWaitSec = 60; //(手动指定游戏进程)等待游戏进程启动的最大时间
    private readonly GalgameCollectionService _galgameService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly JumpListService _jumpListService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IPvnService _pvnService;
    private readonly IFilterService _filterService;
    private readonly ICategoryService _categoryService;
    private readonly IInfoService _infoService;
    [ObservableProperty] private Galgame? _item;
    public ObservableCollection<GalgameViewModelTag> Tags { get; } = new();
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeSavePositionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetExePathCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFromDiskCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetLocalPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedThreadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectProcessCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectTextCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearTextCommand))]
    [ObservableProperty] private bool _isLocalGame; //是否是本地游戏（而非云端同步过来/本地已删除的虚拟游戏）
    [ObservableProperty] private bool _isZipGame;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Visibility _isTagVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isDescriptionVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isCharacterVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isRemoveSelectedThreadVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isSelectProcessVisible = Visibility.Collapsed;
    [ObservableProperty] private bool _canOpenInBgm;
    [ObservableProperty] private bool _canOpenInVndb;
    [ObservableProperty] private bool _canOpenInYmgal;

    [ObservableProperty] private bool _infoBarOpen;
    [ObservableProperty] private string _infoBarMsg = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    private int _msgIndex;
    private bool IsNotLocalGame => !_isLocalGame;
    
    [RelayCommand]
    private void OnCharacterClick(GalgameCharacter? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            _navigationService.NavigateTo(typeof(GalgameCharacterViewModel).FullName!, new GalgameCharacterParameter() {GalgameCharacter = clickedItem});
        }
    }

    [RelayCommand]
    private async Task UnzipGame()
    {
        if (Item?.CheckIsZip() ?? false)
        {
            await _galgameService.ToLocalGalgame(Item);
        }
    }
    
    

    public GalgameViewModel(IGalgameCollectionService dataCollectionService, INavigationService navigationService, 
        IJumpListService jumpListService, ILocalSettingsService localSettingsService, IBgTaskService bgTaskService,
        IPvnService pvnService, IFilterService filterService, ICategoryService categoryService, IInfoService infoService)
    {
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _navigationService = navigationService;
        _galgameService.PhrasedEvent2 += Update;
        _jumpListService = (JumpListService)jumpListService;
        _localSettingsService = localSettingsService;
        _bgTaskService = bgTaskService;
        _pvnService = pvnService;
        _filterService = filterService;
        _categoryService = categoryService;
        _infoService = infoService;
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is not GalgamePageParameter param) //参数不正确，返回主菜单
        {
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
            return;
        }

        Item = param.Galgame;
        IsLocalGame = Item.IsLocalGame;
        IsZipGame = Item.CheckIsZip();
        Item.SavePath = Item.SavePath; //更新存档位置显示
        Update(_item);
        
        if (param.StartGame && await _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart))
            await Play();
        if (param.SelectProgress)
        {
            await Task.Delay(1000);
            await SelectProcess();
        }
    }

    public void OnNavigatedFrom()
    {
        _galgameService.PhrasedEvent2 -= Update;
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
        if (game is null || game != _item) return;
        IsPhrasing = false;
        IsTagVisible = Item?.Tags.Value?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        IsDescriptionVisible = Item?.Description! != string.Empty ? Visibility.Visible : Visibility.Collapsed;
        IsCharacterVisible = Item?.Characters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CanOpenInBgm = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Bangumi]);
        CanOpenInVndb = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Vndb]);
        CanOpenInYmgal = !string.IsNullOrEmpty(Item?.Ids[(int)RssType.Ymgal]);
        IsRemoveSelectedThreadVisible = Item?.ProcessName is not null ? Visibility.Visible : Visibility.Collapsed;
        IsSelectProcessVisible = Item?.ProcessName is null ? Visibility.Visible : Visibility.Collapsed;

        var tagChanged = game.Tags.Value?.Count != Tags.Count;
        for (var i = 0; i < Tags.Count && !tagChanged; i++)
            tagChanged = Tags[i].Tag != game.Tags.Value?[i];
        if (tagChanged)
        {
            Tags.Clear();
            foreach (var tag in game.Tags.Value ?? new())
                Tags.Add(new GalgameViewModelTag
                {
                    Tag = tag,
                    ClickCommand = new RelayCommand(() =>
                    {
                        _filterService.ClearFilters();
                        _filterService.AddFilter(new TagFilter(tag));
                        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
                    }),
                });
        }
    }

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
    private async Task OpenInYmgal()
    {
        if(string.IsNullOrEmpty(Item!.Ids[(int)RssType.Ymgal])) return;
        await Launcher.LaunchUriAsync(new Uri("https://www.ymgal.games/ga"+Item!.Ids[(int)RssType.Ymgal]));
    }
    
    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task Play()
    {
        if (!Item.IsLocalGame) return;
        if (Item.ExePath == null && Item.Startup_parameters==string.Empty)
            await _galgameService.GetGalgameExeAsync(Item);
        if (Item.ExePath == null && Item.Startup_parameters == string.Empty) return;
        Process process;
        if (Item.Startup_parameters == string.Empty)
        {
            process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Item.ExePath,
                    WorkingDirectory = Item.Path,
                    UseShellExecute = Item.RunAsAdmin | Item.ExePath.ToLower().EndsWith("lnk"),
                    Verb = Item.RunAsAdmin ? "runas" : null,
                }
            };
        }
        else
        {
            var pattern = "\".+?\"";
            var regex = new Regex(pattern,RegexOptions.None, TimeSpan.FromSeconds(1));
            MatchCollection matches = regex.Matches(Item.Startup_parameters);
            var filename = matches[0].Value;
            var arguments = Item.Startup_parameters.Replace(filename, "");
            process = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = filename,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    Verb = Item.RunAsAdmin ? "runas" : null,
                    UseShellExecute = true,
                }
            };
        }
        try
        {
            process.Start();
            // _galgameService.Sort();
            if (Item.ProcessName is not null)
            {
                await Task.Delay(1000 * 2); //有可能引导进程和游戏进程是一个名字，等2s让引导进程先退出
                process = await WaitForProcessStartAsync(Item.ProcessName) ?? process;
            }
            if (Item.Startup_parameters != string.Empty && Item.ProcessName is null) { //启动的进程和游戏进程不是同一个进程，需要知道到底启动什么进程
                await Task.Delay(100);
                await SelectProcess();
            }
            _ = _bgTaskService.AddBgTask(new RecordPlayTimeTask(Item, process));
            await _jumpListService.AddToJumpListAsync(Item);
            
            await Task.Delay(1000); //等待1000ms，让游戏进程启动后再最小化
            if(process.HasExited == false)
                App.SetWindowMode(await _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode));
            
            await process.WaitForExitAsync();
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
        if (Item is not {SourceType:GalgameSourceType.LocalFolder}) return;
        await _galgameService.ChangeGalgameSavePosition(Item);
    }
    
    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private void ResetExePath(object obj)
    {
        if (Item is not {SourceType:GalgameSourceType.LocalFolder}) return;
        Item!.ExePath = null;
    }
    
    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task DeleteFromDisk()
    {
        if (Item is not {SourceType:GalgameSourceType.LocalFolder}) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "HomePage_Delete_Title".GetLocalized(),
            Content = "HomePage_Delete_Message".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Secondary
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(Item, true);
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
        };
        await dialog.ShowAsync();
    }
    
    [RelayCommand]
    private async Task OpenInExplorer()
    {
        if(Item == null) return;
        var path = Item.Sources.FirstOrDefault(s => s.SourceType == GalgameSourceType.LocalFolder)?.GetPath(Item);
        if (path is null) //不应该发生
        {
            _infoService.DeveloperEvent(InfoBarSeverity.Error, "Can't find the path of the game");
            return;
        }
        await Launcher.LaunchUriAsync(new Uri(path));
    }

    [RelayCommand]
    private void JumpToPlayedTimePage()
    {
        _navigationService.NavigateTo(typeof(PlayedTimeViewModel).FullName!, Item);
    }

    [RelayCommand]
    private void JumpToHomePageWithDeveloperFilter()
    {
        if (_item is null) return;
        Category? category = _categoryService.GetDeveloperCategory(_item);
        if (category is null)
        {
            _infoService.Info(InfoBarSeverity.Error, msg:"HomePage_NoDeveloperCategory".GetLocalized());
            return;
        }
        _filterService.ClearFilters();
        _filterService.AddFilter(new CategoryFilter(category));
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _galgameService.SaveGalgamesAsync(Item);
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
        Update(_item);
        _ = DisplayMsg(InfoBarSeverity.Success, "GalgamePage_RemoveSelectedThread_Success".GetLocalized());
        await SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task SelectProcess()
    {
        if (!Item.IsLocalGame) return;
        SelectProcessDialog dialog = new();
        await dialog.ShowAsync();
        if (dialog.SelectedProcessName is not null)
        {
            Item.ProcessName = dialog.SelectedProcessName;
            Update(_item);
            await SaveAsync();
            _ = DisplayMsg(InfoBarSeverity.Success, "HomePage_ProcessNameSet".GetLocalized());
        }
    }

    [RelayCommand(CanExecute = nameof(IsLocalGame))]
    private async Task SelectText()
    {
        if (Item is not {SourceType:GalgameSourceType.LocalFolder}) return;
        var path = Item.TextPath;
        if (path is null || File.Exists(path) == false)
        {
            SelectFileDialog dialog = new(Item!.Path, new[] {".txt", ".pdf"}, "GalgamePage_SelectText_Title".GetLocalized());
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

public class GalgameCharacterParameter
{
    [Required] public GalgameCharacter GalgameCharacter = null!;
}

public class GalgameViewModelTag
{
    public required string Tag { get; init; }
    public required IRelayCommand ClickCommand { get; init; }
}