using System.Collections.ObjectModel;
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
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class GalgameFolderViewModel : ObservableObject, INavigationAware
{
    private readonly IDataCollectionService<GalgameFolder> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly IBgTaskService _bgTaskService;
    
    private GalgameFolder? _item;
    public ObservableCollection<Galgame> Galgames = new();
    private readonly List<Galgame> _selectedGalgames = new();
    private GetGalgameInFolderTask? _getGalInFolderTask;
    private GetGalgameInfoFromRss? _getGalgameInfoFromRss;
    private UnpackGameTask? _unpackGameTask;
    public readonly RssType[] RssTypes = { RssType.Bangumi, RssType.Vndb, RssType.Mixed};
    
    [ObservableProperty] private bool _isUnpacking;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _progressMsg = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGalgameCommand))] 
    [NotifyCanExecuteChangedFor(nameof(GetInfoFromRssCommand))]
    [NotifyCanExecuteChangedFor(nameof(GetGalInFolderCommand))]
    private bool _canExecute; //是否正在运行命令
    [ObservableProperty] private bool _logExists; //是否存在日志文件

    [ObservableProperty] private double _titleMaxWidth = 200;
    private double _commandBarWidth;
    private double _pageWidth;

    #region UI_STRING

    [ObservableProperty] private string _uiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();

    #endregion

    public GalgameFolder? Item
    {
        get => _item;

        private set
        {
            SetProperty(ref _item, value);
            if (value != null)
                Galgames = value.GetGalgameList().Result;
        }
    }

    public GalgameFolderViewModel(IDataCollectionService<GalgameFolder> dataCollectionService, 
        IDataCollectionService<Galgame> galgameService, IBgTaskService bgTaskService)
    {
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)galgameService;
        _galgameService.GalgameAddedEvent += ReloadGalgameList;
        _bgTaskService = bgTaskService;
    }

    private void ReloadGalgameList(Galgame galgame)
    {
        if (_item == null) return;
        if (galgame.Path.StartsWith(_item.Path))
            Galgames.Add(galgame);
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not string path) return;
        Item = (_dataCollectionService as GalgameFolderCollectionService)!.GetGalgameFolderFromPath(path);
        if (Item == null) return;
        
        _getGalInFolderTask = _bgTaskService.GetBgTask<GetGalgameInFolderTask>(Item.Path);
        if (_getGalInFolderTask != null)
        {
            _getGalInFolderTask.OnProgress += UpdateNotifyGetGalInFolder;
            UpdateNotifyGetGalInFolder(_getGalInFolderTask.CurrentProgress);
        }
        _unpackGameTask = _bgTaskService.GetBgTask<UnpackGameTask>(Item.Path);
        if (_unpackGameTask != null)
        {
            _unpackGameTask.OnProgress += UpdateNotifyUnpack;
            UpdateNotifyUnpack(_unpackGameTask.CurrentProgress);
        }
        _getGalgameInfoFromRss = _bgTaskService.GetBgTask<GetGalgameInfoFromRss>(Item.Path);
        if (_getGalgameInfoFromRss != null)
        {
            _getGalgameInfoFromRss.OnProgress += UpdateNotifyGetInfoFromRss;
            UpdateNotifyGetGalInFolder(_getGalgameInfoFromRss.CurrentProgress);
        }
        Update();
    }

    public void OnNavigatedFrom()
    {
        _galgameService.GalgameAddedEvent -= ReloadGalgameList;
        if (_getGalInFolderTask != null) _getGalInFolderTask.OnProgress -= UpdateNotifyGetGalInFolder;
        if (_getGalgameInfoFromRss != null) _getGalgameInfoFromRss.OnProgress -= UpdateNotifyGetInfoFromRss;
        if (_unpackGameTask != null)
        {
            _unpackGameTask.OnProgress -= UpdateNotifyGetGalInFolder;
            _unpackGameTask.OnProgress -= HandelUnpackError;
        }
    }

    private void Update()
    {
        if(Item is null) return;
        CanExecute = !Item.IsRunning;
        IsUnpacking = Item.IsUnpacking;
        LogExists = FileHelper.Exists(Item.GetLogPath());
    }

    private void UpdateNotifyUnpack(Progress progress)
    {
        if(Item == null) return;
        Update();
        ProgressValue = (int)((double)progress.Current / progress.Total * 100);
        ProgressMsg = progress.Message;
    }

    private void UpdateNotifyGetGalInFolder(Progress progress)
    {
        if(Item == null) return;
        Update();
        _ = DisplayMsgAsync(progress.ToSeverity(), progress.Message, progress.ToSeverity() switch
        {
            InfoBarSeverity.Informational => 300000,
            _ => 3000
        });
    }
    
    private void UpdateNotifyGetInfoFromRss(Progress progress)
    {
        if(Item == null) return;
        Update();
        _ = DisplayMsgAsync(progress.ToSeverity(), progress.Message, progress.ToSeverity() switch
        {
            InfoBarSeverity.Informational => 300000,
            _ => 3000
        });
    }
    

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task AddGalgame()
    {
        var openPicker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.FileTypeFilter.Add(".exe");
        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
            if (_item!.IsInFolder(folder) == false)
            {
                await ShowGameExistedInfoBar(new Exception("该游戏不属于这个库（游戏必须在库文件夹里面）"));
                return;
            }
            await TryAddGalgame(folder);
        }
    }

    /// <summary>
    /// 试图添加游戏，如果添加失败，会显示错误信息
    /// </summary>
    /// <param name="folder">游戏文件夹路径</param>
    private async Task TryAddGalgame(string folder)
    {
        try
        {
            var result = await _galgameService.TryAddGalgameAsync(folder, true);
            if (result == AddGalgameResult.Success)
                await ShowSuccessInfoBar();
            else if (result == AddGalgameResult.AlreadyExists)
                throw new Exception("库里已经有这个游戏了");
            else //NotFoundInRss
                await ShowNotFoundInfoBar();
        }
        catch (Exception e)
        {
            await ShowGameExistedInfoBar(e);
        }
    }
    
    private async Task ShowGameExistedInfoBar(Exception e)
    {

        IsInfoBarOpen = true;
        InfoBarMessage = e.Message;
        InfoBarSeverity = InfoBarSeverity.Error;
        await Task.Delay(3000);
        IsInfoBarOpen = false;
    }
    private async Task ShowNotFoundInfoBar()
    {
        IsInfoBarOpen = true;
        InfoBarMessage = "成功添加游戏，但没有从信息源中找到这个游戏的信息";
        InfoBarSeverity = InfoBarSeverity.Warning;
        await Task.Delay(3000);
        IsInfoBarOpen = false;
    }
    private async Task ShowSuccessInfoBar()
    {
        IsInfoBarOpen = true;
        InfoBarMessage = "已成功添加游戏到当前库";
        InfoBarSeverity = InfoBarSeverity.Success;
        await Task.Delay(3000);
        IsInfoBarOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private void GetInfoFromRss()
    {
        if (_item == null) return;
        if (_selectedGalgames.Count == 0)
        {
            _getGalgameInfoFromRss = new GetGalgameInfoFromRss(_item);
            _getGalgameInfoFromRss.OnProgress += UpdateNotifyGetInfoFromRss;
            _ = _bgTaskService.AddBgTask(_getGalgameInfoFromRss);
        }
        else
        {
            _getGalgameInfoFromRss = new GetGalgameInfoFromRss(_item, _selectedGalgames);
            _getGalgameInfoFromRss.OnProgress += UpdateNotifyGetInfoFromRss;
            _ = _bgTaskService.AddBgTask(_getGalgameInfoFromRss);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private void GetGalInFolder()
    {
        if (_item == null) return;
        _getGalInFolderTask = new GetGalgameInFolderTask(_item);
        _getGalInFolderTask.OnProgress += UpdateNotifyGetGalInFolder;
        _ = _bgTaskService.AddBgTask(_getGalInFolderTask);
    }
    
    [RelayCommand]
    private async Task AddGalFromZip(string? passWord = null)
    {
        UnpackDialog dialog = new();
        await dialog.ShowAsync();
        StorageFile? file = dialog.StorageFile;

        if (file == null || _item == null) return;

        _unpackGameTask = new UnpackGameTask(file, _item, dialog.GameName, dialog.Password);
        _unpackGameTask.OnProgress += UpdateNotifyUnpack;
        _unpackGameTask.OnProgress += HandelUnpackError;
        _ = _bgTaskService.AddBgTask(_unpackGameTask);
    }

    private async void HandelUnpackError(Progress progress)
    {
        if(progress.ToSeverity() != InfoBarSeverity.Error) return;
        await DisplayMsgAsync(InfoBarSeverity.Error, "GalgameFolder_UnpackGame_Error".GetLocalized());
    }

    [RelayCommand]
    private void OnSelectionChanged(object et)
    {
        SelectionChangedEventArgs e = (SelectionChangedEventArgs) et;
        foreach(Galgame galgame in e.AddedItems)
            _selectedGalgames.Add(galgame);
        foreach (Galgame galgame in e.RemovedItems)
            _selectedGalgames.Remove(galgame);
        UiDownloadInfo = _selectedGalgames.Count == 0
            ? "GalgameFolderPage_DownloadInfo".GetLocalized()
            : "GalgameFolderPage_DownloadSelectedInfo".GetLocalized();
    }

    private void UpdateTitleMaxWidth()
    {
        if (_pageWidth == 0 || _commandBarWidth == 0) return;
        TitleMaxWidth = Math.Max(_pageWidth - _commandBarWidth - 20, 0);
    }
    
    [RelayCommand]
    private void OnPageSizeChanged(SizeChangedEventArgs e)
    {
        _pageWidth = e.NewSize.Width;
        UpdateTitleMaxWidth();
    }

    [RelayCommand]
    private void OnCommandBarSizeChanged(SizeChangedEventArgs e)
    {
        _commandBarWidth = e.NewSize.Width;
        UpdateTitleMaxWidth();
    }

    [RelayCommand]
    private async Task ViewLog()
    {
        if(Item is null) return;
        var path = Item.GetLogPath();
        if(FileHelper.Exists(path) == false) return; 
        await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(FileHelper.GetFullPath(path)));
    }
    
    #region INFO_BAR_CTRL

    private int _infoBarIndex;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

    /// <summary>
    /// 使用InfoBar显示信息
    /// </summary>
    /// <param name="infoBarSeverity">信息严重程度</param>
    /// <param name="msg">信息</param>
    /// <param name="delayMs">显示时长(ms)</param>
    private async Task DisplayMsgAsync(InfoBarSeverity infoBarSeverity, string msg, int delayMs = 3000)
    {
        var currentIndex = ++_infoBarIndex;
        InfoBarSeverity = infoBarSeverity;
        InfoBarMessage = msg;
        IsInfoBarOpen = true;
        await Task.Delay(delayMs);
        if (currentIndex == _infoBarIndex)
            IsInfoBarOpen = false;
    }

    #endregion
}