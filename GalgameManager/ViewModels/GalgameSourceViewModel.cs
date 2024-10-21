using System.Collections.ObjectModel;
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

namespace GalgameManager.ViewModels;

public partial class GalgameSourceViewModel : ObservableObject, INavigationAware
{
    private readonly IGalgameSourceCollectionService _sourceService;
    private readonly GalgameCollectionService _galgameService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IInfoService _infoService;
    
    private GalgameSourceBase? _item;
    public ObservableCollection<GalgameAndPath> Galgames { get; } = new();
    private readonly List<Galgame> _selectedGalgames = new();
    private BgTaskBase? _getGalTask;
    private GetGalgameInfoFromRssTask? _getGalgameInfoFromRss;
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

    public GalgameSourceBase? Item
    {
        get => _item;

        private set
        {
            if (_item is not null) _item.GalgamesChanged -= ReloadGalgameList;
            SetProperty(ref _item, value);
            if (value != null)
            {
                Galgames.SyncCollection(value.Galgames);
                value.GalgamesChanged += ReloadGalgameList;
            }
        }
    }

    public GalgameSourceViewModel(IGalgameSourceCollectionService dataCollectionService, 
        IGalgameCollectionService galgameService, IBgTaskService bgTaskService, IInfoService infoService)
    {
        _sourceService = dataCollectionService;
        _galgameService = (GalgameCollectionService)galgameService;
        _bgTaskService = bgTaskService;
        _infoService = infoService;
    }

    private void ReloadGalgameList(Galgame game, bool isDeleted)
    {
        if (_item == null) return;
        Galgames.SyncCollection(_item.Galgames);
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not string url) return;
        //TODO
        Item = _sourceService.GetGalgameSourceFromUrl(url);
        if (Item == null) return;
        
        _getGalTask = _bgTaskService.GetBgTask<GetGalgameInSourceTask>(Item.Url);
        if (_getGalTask != null)
        {
            _getGalTask.OnProgress += UpdateNotifyGetGal;
            UpdateNotifyGetGal(_getGalTask.CurrentProgress);
        }
        
        _unpackGameTask = _bgTaskService.GetBgTask<UnpackGameTask>(Item.Url);
        if (_unpackGameTask != null)
        {
            _unpackGameTask.OnProgress += UpdateNotifyUnpack;
            UpdateNotifyUnpack(_unpackGameTask.CurrentProgress);
        }
        _getGalgameInfoFromRss = _bgTaskService.GetBgTask<GetGalgameInfoFromRssTask>(Item.Url);
        if (_getGalgameInfoFromRss != null)
        {
            _getGalgameInfoFromRss.OnProgress += UpdateNotifyGetInfoFromRss;
            UpdateNotifyGetGal(_getGalgameInfoFromRss.CurrentProgress);
        }
        Update();
    }

    public void OnNavigatedFrom()
    {
        if (_getGalTask != null) _getGalTask.OnProgress -= UpdateNotifyGetGal;
        if (_getGalgameInfoFromRss != null) _getGalgameInfoFromRss.OnProgress -= UpdateNotifyGetInfoFromRss;
        if (_unpackGameTask != null)
        {
            _unpackGameTask.OnProgress -= UpdateNotifyGetGal;
            _unpackGameTask.OnProgress -= HandelUnpackError;
        }
        Item = null; //确保监听注销
    }

    private void Update()
    {
        if(Item is null) return;
        CanExecute = !Item.IsRunning;
        IsUnpacking = _bgTaskService.GetBgTask<UnpackGameTask>(Item.Path)?.IsRunning ?? false;
        LogExists = FileHelper.Exists(Item.GetLogPath());
    }

    private void UpdateNotifyUnpack(Progress progress)
    {
        if(Item == null) return;
        Update();
        ProgressValue = (int)((double)progress.Current / progress.Total * 100);
        ProgressMsg = progress.Message;
    }

    private void UpdateNotifyGetGal(Progress progress)
    {
        if(Item == null) return;
        Update();
        _infoService.Info(progress.ToSeverity(), msg: progress.Message, displayTimeMs: progress.ToSeverity() switch
        {
            InfoBarSeverity.Informational => 300000,
            _ => 3000
        });
    }
    
    private void UpdateNotifyGetInfoFromRss(Progress progress)
    {
        if(Item == null) return;
        Update();
        _infoService.Info(progress.ToSeverity(), msg: progress.Message, displayTimeMs: progress.ToSeverity() switch
        {
            InfoBarSeverity.Informational => 300000,
            _ => 3000
        });
    }
    

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task AddGalgame()
    {
        //TODO
        FileOpenPicker openPicker = new();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.FileTypeFilter.Add(".exe");
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            var folder = file.Path.Substring(0, Math.Max(file.Path.LastIndexOf('\\'), 0));
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
            if (!Item!.IsInSource(folder))
            {
                _infoService.Info(InfoBarSeverity.Error, msg:"GalgameSourcePage_NotInSource".GetLocalized());
                return;
            }
            Galgame game = await _galgameService.AddGameAsync(Item!.SourceType, folder, true);
            if (game.IsIdsEmpty())
                _infoService.Info(InfoBarSeverity.Warning, msg: "AddGalgameResult_NotFoundInRss".GetLocalized());
            else
                _infoService.Info(InfoBarSeverity.Success, msg: "AddGalgameResult_Success".GetLocalized());
        }
        catch (Exception e)
        { 
            _infoService.Info(InfoBarSeverity.Error, msg: e.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private void GetInfoFromRss()
    {
        if (_item == null) return;
        if (_selectedGalgames.Count == 0)
        {
            _getGalgameInfoFromRss = new GetGalgameInfoFromRssTask(_item);
            _getGalgameInfoFromRss.OnProgress += UpdateNotifyGetInfoFromRss;
            _ = _bgTaskService.AddBgTask(_getGalgameInfoFromRss);
        }
        else
        {
            _getGalgameInfoFromRss = new GetGalgameInfoFromRssTask(_item, _selectedGalgames);
            _getGalgameInfoFromRss.OnProgress += UpdateNotifyGetInfoFromRss;
            _ = _bgTaskService.AddBgTask(_getGalgameInfoFromRss);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private void GetGalInFolder()
    {
        if (_item == null) return;
        //TODO
        _getGalTask = new GetGalgameInSourceTask(_item);
        _getGalTask.OnProgress += UpdateNotifyGetGal;
        _ = _bgTaskService.AddBgTask(_getGalTask);
    }
    
    [RelayCommand(CanExecute = nameof(IsLocalFolder))]
    private async Task AddGalFromZip(string? passWord = null)
    {
        if (_item is not GalgameFolderSource) return;
        UnpackDialog dialog = new();
        await dialog.ShowAsync();
        StorageFile? file = dialog.StorageFile;

        if (file == null || _item == null) return;

        _unpackGameTask = new UnpackGameTask(file, Item!.Path, dialog.GameName, dialog.Password);
        _unpackGameTask.OnProgress += UpdateNotifyUnpack;
        _unpackGameTask.OnProgress += HandelUnpackError;
        _ = _bgTaskService.AddBgTask(_unpackGameTask);
    }

    private bool IsLocalFolder()
    {
        return Item?.SourceType == GalgameSourceType.LocalFolder;
    }

    private void HandelUnpackError(Progress progress)
    {
        if(progress.ToSeverity() != InfoBarSeverity.Error) return;
        _infoService.Info(InfoBarSeverity.Error, msg:"GalgameFolder_UnpackGame_Error".GetLocalized());
    }

    [RelayCommand]
    private void OnSelectionChanged(object et)
    {
        SelectionChangedEventArgs e = (SelectionChangedEventArgs) et;
        foreach(GalgameAndPath g in e.AddedItems)
            _selectedGalgames.Add(g.Galgame);
        foreach (GalgameAndPath g in e.RemovedItems)
            _selectedGalgames.Remove(g.Galgame);
        UiDownloadInfo = _selectedGalgames.Count == 0
            ? "GalgameFolderPage_DownloadInfo".GetLocalized()
            : "GalgameFolderPage_DownloadSelectedInfo".GetLocalized();
    }

    private void UpdateTitleMaxWidth()
    {
        if (_pageWidth == 0 || _commandBarWidth == 0) return;
        TitleMaxWidth = Math.Max(_pageWidth - _commandBarWidth - 20, 0) / 2;
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
}