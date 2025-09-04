using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
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
using Windows.ApplicationModel.DataTransfer;

namespace GalgameManager.ViewModels;

public partial class GalgameSourceViewModel : ObservableObject, INavigationAware
{
    private readonly IGalgameSourceCollectionService _sourceService;
    private readonly GalgameCollectionService _galgameService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IInfoService _infoService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _settingsService;
    private readonly ISourceScanResultService _sourceScanService;
    private static readonly List<GetGalgameInfoFromRssTask> RssTasks = [];

    private GalgameSourceBase? _item;
    public AdvancedCollectionView Galgames { get; } = new(new ObservableCollection<GalgameAndPath>(), true);
    public List<RssType> RssTypes { get; } = new(){RssType.Bangumi, RssType.Vndb, RssType.Ymgal, RssType.Cngal, RssType.Mixed, RssType.None};
    private readonly List<Galgame> _selectedGalgames = new();
    private UnpackGameTask? _unpackGameTask;
    
    [ObservableProperty] private bool _isUnpacking;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _progressMsg = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetInfoFromRssCommand), nameof(ScanAllCommand))]
    private bool _canExecute; //是否正在运行命令
    [ObservableProperty] private bool _logExists; //是否存在日志文件
    [ObservableProperty] private bool _saveMetadata;

    [ObservableProperty] private double _titleMaxWidth = 200;
    [ObservableProperty] private double _gameListHeight;
    [ObservableProperty] private bool _gameListExpend;
    private double _commandBarWidth;
    private double _pageWidth;

    [ObservableProperty] private bool _includeSubSources;
    public ObservableCollection<string> DontScanPaths = [];
    
    #region SORTING
    // 为XAML绑定添加静态枚举值属性
    public SortKeys NameSortKey => SortKeys.Name;
    public SortKeys LastPlaySortKey => SortKeys.LastPlay;
    public SortKeys DeveloperSortKey => SortKeys.Developer;
    public SortKeys RatingSortKey => SortKeys.Rating;
    public SortKeys ReleaseDateSortKey => SortKeys.ReleaseDate;
    public SortKeys LastFetchInfoTimeSortKey => SortKeys.LastFetchInfoTime;
    public SortKeys AddTimeSortKey => SortKeys.AddTime;
    public SortKeys PathSortKey => SortKeys.Path;

    [ObservableProperty] private SortKeys _currentSortKey = SortKeys.Name;
    [ObservableProperty] private bool _sortDescending = true;
    
    [RelayCommand]
    private void Sort(SortKeys sortKey)
    {
        // 如果点击当前排序键，则切换排序方向
        if (CurrentSortKey == sortKey)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            CurrentSortKey = sortKey;
        }
        
        ApplySorting();
    }
    
    [RelayCommand]
    private void ApplySorting()
    {
        Galgames.SortDescriptions.Clear();
        var direction = SortDescending ? SortDirection.Descending : SortDirection.Ascending;
        switch (CurrentSortKey)
        {
            case SortKeys.Name:
                Galgames.SortDescriptions.Add(new SortDescription("NameForSort", direction, StringComparer.CurrentCultureIgnoreCase));
                break;
            case SortKeys.LastPlay:
                Galgames.SortDescriptions.Add(new SortDescription("LastPlayTimeForSort", direction));
                break;
            case SortKeys.Developer:
                Galgames.SortDescriptions.Add(new SortDescription("DeveloperForSort", direction, StringComparer.CurrentCultureIgnoreCase));
                break;
            case SortKeys.Rating:
                Galgames.SortDescriptions.Add(new SortDescription("RatingForSort", direction));
                break;
            case SortKeys.ReleaseDate:
                Galgames.SortDescriptions.Add(new SortDescription("ReleaseDateForSort", direction));
                break;
            case SortKeys.AddTime:
                Galgames.SortDescriptions.Add(new SortDescription("AddTimeForSort", direction));
                break;
            case SortKeys.Path:
                Galgames.SortDescriptions.Add(new SortDescription("PathForSort", direction, StringComparer.CurrentCultureIgnoreCase));
                break;
        }

        Galgames.RefreshSorting();

        _ = Task.Run(async () =>
        {
            await _settingsService.SaveSettingAsync(KeyValues.LibrarySortKey, (int)CurrentSortKey);
            await _settingsService.SaveSettingAsync(KeyValues.LibraryGameSortDescending, SortDescending);
        });
    }
    #endregion

    #region UI_STRING

    [ObservableProperty] private string _uiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();
    [ObservableProperty] private bool _isDownloadFromNameVisible;

    public string ImagePathDes => Item?.ImagePath ?? "GalgameSourcePage_Setting_NoImage".GetLocalized();

    #endregion

    public GalgameSourceBase? Item
    {
        get => _item;

        private set
        {
            if (_item is not null)
            {
                _item.GalgamesChanged -= ReloadGalgameList;
                _item.PropertyChanged -= Save;
            }
            SetProperty(ref _item, value);
            if (value != null)
            {
                LoadGames();
                value.GalgamesChanged += ReloadGalgameList;
                value.PropertyChanged += Save;
                SaveMetadata = value.SaveMetaBackup;
            }
            OnPropertyChanged(nameof(LogExists));
        }
    }

    public GalgameSourceViewModel(IGalgameSourceCollectionService dataCollectionService, 
        IGalgameCollectionService galgameService, IBgTaskService bgTaskService, IInfoService infoService, 
        INavigationService navigationService, ILocalSettingsService settingsService, 
        ISourceScanResultService sourceScanService)
    {
        _sourceService = dataCollectionService;
        _galgameService = (GalgameCollectionService)galgameService;
        _bgTaskService = bgTaskService;
        _infoService = infoService;
        _navigationService = navigationService;
        _settingsService = settingsService;
        _sourceScanService = sourceScanService;
    }

    private void LoadGames()
    {
        if (_item == null)
        {
            Galgames.Clear();
            return;
        }

        List<GalgameAndPath> target = new();
        // 当前库
        foreach (GalgameAndPath g in _item.Galgames)
            target.Add(new GalgameAndPath(g.Galgame, g.Path));

        // 子库（可选）
        if (IncludeSubSources)
            LoadFromSubSources(_item, target);

        // 去重（按 Galgame 实例）
        List<GalgameAndPath> distinct = new();
        HashSet<Galgame> seen = new();
        foreach (var g in target)
        {
            if (seen.Add(g.Galgame))
                distinct.Add(g);
        }

        // 刷新底层源并应用排序
        var source = (ObservableCollection<GalgameAndPath>)Galgames.Source;
        source.Clear();
        foreach (var t in distinct)
            source.Add(t);
        Galgames.RefreshSorting();

        // 加载不扫描路径列表
        DontScanPaths.Clear();
        foreach (var path in _item.DontScanPath)
            DontScanPaths.Add(path);
    }
    
    private void LoadFromSubSources(GalgameSourceBase source, List<GalgameAndPath> target)
    {
        foreach (GalgameSourceBase sub in source.SubSources)
        {
            foreach (GalgameAndPath g in sub.Galgames)
                target.Add(new GalgameAndPath(g.Galgame, g.Path));
            LoadFromSubSources(sub, target);
        }
    }
    
    private void ReloadGalgameList(Galgame game, bool isDeleted)
    {
        if (_item == null) return;
        var source = (ObservableCollection<GalgameAndPath>)Galgames.Source;
        if (isDeleted && source.FirstOrDefault(g => g.Galgame == game) is { } tmp)
            source.Remove(tmp);
        else if (!isDeleted)
        {
            // 检查游戏是在当前库还是子库中
            var path = _item.GetPath(game);
            if (path != null)
            {
                source.Add(new GalgameAndPath(game, path));
            }
            else if (IncludeSubSources)
            {
                // 递归检查子库
                CheckSubSourcesForGame(_item, game);
            }
        }
        // 刷新排序
        Galgames.RefreshSorting();
    }

    private bool CheckSubSourcesForGame(GalgameSourceBase source, Galgame game)
    {
        foreach (var subSource in source.SubSources)
        {
            var path = subSource.GetPath(game);
            if (path != null)
            {
                var src = (ObservableCollection<GalgameAndPath>)Galgames.Source;
                src.Add(new GalgameAndPath(game, path));
                return true;
            }
            
            if (CheckSubSourcesForGame(subSource, game))
            {
                return true;
            }
        }
        return false;
    }

    public void OnNavigatedTo(object parameter)
    {
        try
        {
            IncludeSubSources = _settingsService.ReadSettingAsync<bool>(KeyValues.GalgameSourcePageShowSubSourceGames).Result;
        
            // 加载排序设置
            CurrentSortKey = (SortKeys)_settingsService.ReadSettingAsync<int>(KeyValues.LibrarySortKey).Result;
            SortDescending = _settingsService.ReadSettingAsync<bool>(KeyValues.LibraryGameSortDescending).Result;
            ApplySorting();
        
            if (parameter is not string url) return;
            Item = _sourceService.GetGalgameSourceFromUrl(url);
            if (Item == null) return;
        
            _unpackGameTask = _bgTaskService.GetBgTask<UnpackGameTask>(Item.Url);
            if (_unpackGameTask != null)
            {
                _unpackGameTask.OnProgress += UpdateNotifyUnpack;
                UpdateNotifyUnpack(_unpackGameTask.CurrentProgress);
            }
            foreach (GetGalgameInfoFromRssTask task in RssTasks.Where(t => t.IsRunning))
                task.OnProgress += HandleGetGalInfoProgressChanged;
            Update();
            LogExists = Item is not null && _sourceScanService.GetScanResult(Item.Id) is not null;
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    public void OnNavigatedFrom()
    {
        if (_unpackGameTask != null)
        {
            _unpackGameTask.OnProgress -= UpdateNotifyUnpack;
            _unpackGameTask.OnProgress -= HandelUnpackError;
        }

        foreach (GetGalgameInfoFromRssTask task in RssTasks)
            task.OnProgress -= HandleGetGalInfoProgressChanged;
        List<GetGalgameInfoFromRssTask> toRemove = RssTasks.Where(t => !t.IsRunning).ToList();
        foreach (GetGalgameInfoFromRssTask task in toRemove) RssTasks.Remove(task);

        Item = null; //确保监听注销
    }

    private void Update()
    {
        if(Item is null) return;
        CanExecute = !Item.IsRunning;
        IsUnpacking = _bgTaskService.GetBgTask<UnpackGameTask>(Item.Path)?.IsRunning ?? false;
        // LogExists = _sourceScanService.GetScanResultAsync(Item.Id).Result is not null;
    }

    private void UpdateNotifyUnpack(Progress progress)
    {
        if(Item == null) return;
        Update();
        ProgressValue = (int)((double)progress.Current / progress.Total * 100);
        ProgressMsg = progress.Message;
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task GetInfoFromRss(object parameter)
    {
        if (Item == null) return;
        

        // 检查是否是 从游戏名下载信息 模式
        if (parameter is string isNameOnly && isNameOnly == "True")
        {
            // 清除目前存储的id信息
            foreach (var galgame in _selectedGalgames)
            {
                for (var i = 0; i < Galgame.PhraserNumber; i++)
                {
                    // 跳过potato
                    if (i == (int)RssType.PotatoVn)
                        continue;
                    galgame.Ids[i] = null;
                }
            }
        }
        
        SelectGameInfoToFetchDialog selectInfoDialog = new(_selectedGalgames.Count == 0);
        await selectInfoDialog.ShowAsync();
        if (selectInfoDialog.Canceled) return;
        var scanSubfolders = selectInfoDialog.IncludingSubSources;
        GameParseType selectedParseTypes = selectInfoDialog.SelectedParseTypes;

        // 选择了游戏范围
        if (_selectedGalgames.Count > 0)
        {
            var getGalgameInfoFromRss = new GetGalgameInfoFromRssTask(Item, _selectedGalgames);
            getGalgameInfoFromRss.OnProgress += HandleGetGalInfoProgressChanged;
            RssTasks.Add(getGalgameInfoFromRss);
            _ = _bgTaskService.AddBgTask(getGalgameInfoFromRss);
            return;
        }
        //else，下载整个库（或包括子库）
        List<GalgameSourceBase> sources = scanSubfolders ? [Item] : Item.GetSubSourcesRecursive();
        foreach (GalgameSourceBase source in sources)
        {
            var getGalgameInfoFromRss = new GetGalgameInfoFromRssTask(source, selectedParseTypes);
            getGalgameInfoFromRss.OnProgress += HandleGetGalInfoProgressChanged;
            RssTasks.Add(getGalgameInfoFromRss);
            _ = _bgTaskService.AddBgTask(getGalgameInfoFromRss);
        }
    }

    private void HandleGetGalInfoProgressChanged(Progress progress)
    {
        Update();
        _infoService.Info(progress.ToSeverity(), msg: progress.Message, displayTimeMs: progress.ToSeverity() switch
        {
            InfoBarSeverity.Informational => 300000,
            _ => 3000
        });
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ScanAll()
    {
        // 和 LibraryViewModel 中的 ScanAll() 基本一致
        if (Item == null) return;
        // 创建确认对话框
        CheckBox includeSubfoldersCheckBox = new()
        {
            Content = "LibraryPage_ScanAll_IncludeSubfolders".GetLocalized(),
            IsChecked = true
        };

        StackPanel dialogContent = new()
        {
            Spacing = 10
        };
        dialogContent.Children.Add(new TextBlock { Text = "LibraryPage_ScanAll_Content".GetLocalized() });
        dialogContent.Children.Add(includeSubfoldersCheckBox);

        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "LibraryPage_ScanAll_Title".GetLocalized(),
            Content = dialogContent,
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var scanSubfolders = includeSubfoldersCheckBox.IsChecked ?? true;

        // 获取当前目录下所有库
        List<GalgameSourceBase> sources = new();
        sources.Add(Item);

        // 如果用户选择包含子文件夹，则添加所有子库
        if (scanSubfolders)
        {
            var allSources = _sourceService.GetGalgameSources();
            AddSubSources(Item, allSources);
        }

        foreach (var source in sources)
        {
            Update();
            _sourceService.Scan(source);
            _infoService.Info(InfoBarSeverity.Success, msg: "LibraryPage_Scan_Success".GetLocalized(source.Name));
        }

        return;

        void AddSubSources(GalgameSourceBase parent, IEnumerable<GalgameSourceBase> allSources)
        {
            foreach (var source in allSources.Where(s => s.ParentSource == parent))
            {
                sources.Add(source);
                AddSubSources(source, allSources);
            }
        }
        
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
    
    [RelayCommand]
    private async Task SetImagePath(bool reset = false)
    {
        if (Item is null) return;
        if (reset)
            Item.ImagePath = null;
        else
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".bmp");
            openPicker.FileTypeFilter.Add(".webp");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file != null)
                Item.ImagePath = file.Path;
        }
        OnPropertyChanged(nameof(ImagePathDes));
    }

    private void Save(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        if (Item is null) return;
        _sourceService.Save(Item);
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
        if (_selectedGalgames.Count != 0)
            IsDownloadFromNameVisible = true;
        else
            IsDownloadFromNameVisible = false;
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
        GameListHeight = Math.Max(e.NewSize.Height - 200, 0);
    }

    [RelayCommand]
    private void OnCommandBarSizeChanged(SizeChangedEventArgs e)
    {
        _commandBarWidth = e.NewSize.Width;
        UpdateTitleMaxWidth();
    }
    
    [RelayCommand]
    private void ViewLog()
    {
        if(Item is null || !LogExists) return;
        _navigationService.NavigateTo(typeof(ScanResultViewModel).FullName!, Item.Id);
    }

    [RelayCommand]
    private async Task DeleteSingleGame(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "GalgameSourcePage_Remove_Title".GetLocalized(),
            Content = string.Format("GalgameSourcePage_Remove_SingleGame".GetLocalized(), gameAndPath.Galgame.Name.Value),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _galgameService.RemoveGalgame(gameAndPath.Galgame);
            // 如果当前游戏在选中列表中，也要将其移除
            if (_selectedGalgames.Contains(gameAndPath.Galgame))
            {
                _selectedGalgames.Remove(gameAndPath.Galgame);
                // 更新UI状态
                if (_selectedGalgames.Count == 0)
                {
                    UiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();
                    IsDownloadFromNameVisible = false;
                }
            }
        }
    }

    [RelayCommand]
    private async Task DeleteGame()
    {
        if (_selectedGalgames.Count == 0) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "GalgameSourcePage_Remove_Title".GetLocalized(),
            Content = string.Format("GalgameSourcePage_Remove_Message".GetLocalized(), _selectedGalgames.Count),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            // 创建选中游戏的副本，避免在遍历过程中集合被修改
            List<Galgame> gamesToRemove = new(_selectedGalgames);
            foreach (var galgame in gamesToRemove)
            {
                await _galgameService.RemoveGalgame(galgame);
            }
            // 操作完成后清空选中集合
            _selectedGalgames.Clear();
            UiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();
            IsDownloadFromNameVisible = false;
        };
    
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private void EditGame(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        // 在主线程中执行导航，修复appbarbutton描述文字延迟显示的问题
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
            _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, gameAndPath.Galgame)
        );
    }

    [RelayCommand]
    private void CopyGameName(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(gameAndPath.Galgame.Name.Value);
        Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    private void CopyGamePath(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(gameAndPath.Path);
        Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    private async Task OpenGameInExplorer(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        var folder = await StorageFolder.GetFolderFromPathAsync(gameAndPath.Galgame.LocalPath);
        await Launcher.LaunchFolderAsync(folder);
    }

    partial void OnIncludeSubSourcesChanged(bool value)
    {
        if (_item != null)
        {
            _settingsService.SaveSettingAsync(KeyValues.GalgameSourcePageShowSubSourceGames, value);
            LoadGames();
        }
    }

    async partial void OnSaveMetadataChanged(bool value)
    {
        try
        {
            if (Item == null) return; //不应该发生
            if (value == Item.SaveMetaBackup) return;
            Item.SaveMetaBackup = value;
            if (value)
            {
                foreach (Galgame galgame in Item.GetGalgameList())
                {
                    _infoService.Info(InfoBarSeverity.Informational,
                        msg: "GalgameSourcePage_SavingMeta".GetLocalized(galgame.Name.Value ?? string.Empty));
                    await _galgameService.SaveGalgameMetaAsync(galgame, Item);
                }
                _infoService.Info(InfoBarSeverity.Success, msg: "GalgameSourcePage_SaveMetaSuccess".GetLocalized());
            }
            else
            {
                foreach (Galgame game in Item.GetGalgameList())
                {
                    _infoService.Info(InfoBarSeverity.Informational,
                        msg: "GalgameSourcePage_RemovingMeta".GetLocalized(game.Name.Value ?? string.Empty));
                    await SourceServiceFactory.GetSourceService(Item.SourceType).RemoveMetaAsync(game);
                }
                _infoService.Info(InfoBarSeverity.Success, msg: "GalgameSourcePage_RemoveMetaSuccess".GetLocalized());
            }
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.ToString());
        } 
    }

    [RelayCommand]
    private void RemoveDontScanPath(string path)
    {
        if (Item == null) return;
        DontScanPaths.Remove(path);
        Item.DontScanPath.Remove(path);
        _sourceService.Save(Item);
    }

    [RelayCommand]
    private async Task AddDontScanPath()
    {
        if (Item is null) return;
        try
        {
            await new SelectToScanFolderDialog(Item).ShowAsync();
            DontScanPaths.SyncCollection(Item.DontScanPath);
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e is PvnException ? e.Message : e.ToString());
        }
    }
}