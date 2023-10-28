using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using GalgameManager.Helpers.Converter;

namespace GalgameManager.ViewModels;

public partial class HomeViewModel : ObservableRecipient, INavigationAware
{
    private const int SearchDelay = 500;
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    private IFilter? _filter; //进入界面时的使用的过滤器，退出界面后移除
    private DateTime _lastSearchTime = DateTime.Now;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Stretch _stretch;
    [ObservableProperty] private string _searchKey = string.Empty;
    [ObservableProperty] private string _searchTitle = string.Empty;
    [ObservableProperty] private bool _fixHorizontalPicture; // 是否修复横向图片（截断为标准的长方形）
    [ObservableProperty] private bool _displayPlayTypePolygon = true; // 是否显示游玩状态的小三角形
    [ObservableProperty] private bool _displayVirtualGame; //是否显示虚拟游戏
    [ObservableProperty] private bool _specialDisplayVirtualGame; //是否特殊显示虚拟游戏（降低透明度）

    #region UI

    private static readonly ResourceLoader ResourceLoader= new();
    public readonly string UiEdit = ResourceLoader.GetString("HomePage_Edit");
    public readonly string UiDownLoad = ResourceLoader.GetString("HomePage_Download");
    public readonly string UiRemove = ResourceLoader.GetString("HomePage_Remove");
    public readonly string UiAddNewGame = ResourceLoader.GetString("HomePage_AddNewGame");
    public readonly string UiSort = ResourceLoader.GetString("HomePage_Sort");
    public readonly string UiFilter = ResourceLoader.GetString("HomePage_Filter");
    private readonly string _uiSearch = "HomePage_Search_Label".GetLocalized();

    private readonly string _uiRemoveTitle = ResourceLoader.GetString("HomePage_Remove_Title");
    private readonly string _uiRemoveMessage = ResourceLoader.GetString("HomePage_Remove_Message");
    private readonly string _uiYes = ResourceLoader.GetString("Yes");
    private readonly string _uiCancel = ResourceLoader.GetString("Cancel");

    #endregion

    public ICommand ItemClickCommand
    {
        get;
    }

    public ObservableCollection<Galgame> Source { get; private set; } = new();

    public HomeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> dataCollectionService,
        ILocalSettingsService localSettingsService, IFilterService filterService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)_dataCollectionService;
        _localSettingsService = localSettingsService;
        _filterService = filterService;

        _galgameService.GalgameLoadedEvent += OnGalgameLoadedEvent;
        _galgameService.PhrasedEvent += OnGalgameServicePhrased;
        _galgameService.SyncProgressChanged += OnSyncChanged;

        _stretch = localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result
            ? Stretch.UniformToFill : Stretch.Uniform;
        _fixHorizontalPicture = localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result;
        DisplayPlayTypePolygon = localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon).Result;
        DisplayVirtualGame = localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayVirtualGame).Result;
        SpecialDisplayVirtualGame = localSettingsService.ReadSettingAsync<bool>(KeyValues.SpecialDisplayVirtualGame).Result;
        GameToOpacityConverter.SpecialDisplayVirtualGame = SpecialDisplayVirtualGame;

        ItemClickCommand = new RelayCommand<Galgame>(OnItemClick);
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        SearchKey = _galgameService.GetSearchKey();
        UpdateSearchTitle();
        if(parameter is IFilter filter)
        {
            _filter = filter;
            _filterService.AddFilter(_filter);
        }
        Source = await _dataCollectionService.GetContentGridDataAsync();
    }

    public async void OnNavigatedFrom()
    {
        if (_filter is not null)
        {
            await Task.Delay(200); //等待动画结束
            _filterService.RemoveFilter(_filter);
        }
        _galgameService.PhrasedEvent -= OnGalgameServicePhrased;
        _galgameService.SyncProgressChanged -= OnSyncChanged;
        _galgameService.GalgameLoadedEvent -= OnGalgameLoadedEvent;
    }

    private void OnItemClick(Galgame? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            object param = string.IsNullOrEmpty(clickedItem.Path) ? clickedItem : clickedItem.Path;
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, param);
        }
    }

    #region DRAG_AND_DROP

    [ObservableProperty] private bool _displayDragArea;
    
    public async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();
            if (items.Count <= 0) return;
            foreach (IStorageItem storageItem in items)
            {
                StorageFile item = (StorageFile)storageItem;
                var folder = item.Path.Substring(0, item.Path.LastIndexOf('\\'));
                _ =  AddGalgameInternal(folder);
            }
        }
        DisplayDragArea = false;
    }

    public void Grid_DragEnter(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Link;
        DisplayDragArea = true;
    }
    
    public void Grid_DragLeave(object sender, DragEventArgs e)
    {
        DisplayDragArea = false;
    }

    #endregion
    
    /// <summary>
    /// 添加Galgame
    /// </summary>
    /// <param name="path">游戏文件夹路径</param>
    private async Task AddGalgameInternal(string path)
    {
        IsPhrasing = true;
        AddGalgameResult result = AddGalgameResult.Other;
        string msg;
        try
        {
            result = await _galgameService.TryAddGalgameAsync(path, true);
            msg = result.ToMsg();
        }
        catch (Exception e)
        {
            msg = e.Message;
        }
        IsPhrasing = false;
        _ = DisplayMsgAsync(result.ToInfoBarSeverity(), msg);
    }

    private void OnSyncChanged((int cnt, int total) tuple)
    {
        if (tuple.total == 0) return;
        _ = tuple.cnt == tuple.total
            ? DisplayMsgAsync(InfoBarSeverity.Success, string.Format("HomePage_Synced".GetLocalized(), tuple.total))
            : DisplayMsgAsync(InfoBarSeverity.Informational, string.Format("HomePage_Syncing".GetLocalized(), tuple.cnt, tuple.total), 120*1000);
    }
    
    private void OnGalgameServicePhrased() => IsPhrasing = false;
    
    private async void OnGalgameLoadedEvent() => Source = await _galgameService.GetContentGridDataAsync();

    [RelayCommand]
    private async Task AddGalgame()
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
                await AddGalgameInternal(folder);
            }
        }
        catch (Exception e)
        {
            _ = DisplayMsgAsync(InfoBarSeverity.Error, e.Message);
        }
    }
    
    [RelayCommand]
    private async Task Sort()
    
    {
        await _galgameService.SetSortKeysAsync();
    }

    [RelayCommand]
    private async Task Search(object et)
    {
        _lastSearchTime = DateTime.Now;
        DateTime tmp = _lastSearchTime;
        await Task.Delay(SearchDelay);
        if (tmp == _lastSearchTime) //如果在延迟时间内没有再次输入，则开始搜索
        {
            _galgameService.Search(SearchKey);
            UpdateSearchTitle();
        }
    }

    private void UpdateSearchTitle()
    {
        SearchTitle = SearchKey == string.Empty ? _uiSearch : _uiSearch + " ●";
    }

    [RelayCommand]
    private async Task GalFlyOutDelete(Galgame? galgame)
    {
        if(galgame == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = _uiRemoveTitle,
            Content = _uiRemoveMessage,
            PrimaryButtonText = _uiYes,
            SecondaryButtonText = _uiCancel
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(galgame, true);
        };
        
        await dialog.ShowAsync();
    }
    
    [RelayCommand]
    private void GalFlyOutEdit(Galgame? galgame)
    {
        if(galgame == null) return;
        _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame);
    }

    [RelayCommand]
    private async Task GalFlyOutGetInfoFromRss(Galgame? galgame)
    {
        if(galgame == null) return;
        IsPhrasing = true;
        await _galgameService.PhraseGalInfoAsync(galgame);
        IsPhrasing = false;
    }

    partial void OnFixHorizontalPictureChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.FixHorizontalPicture, value);
        Stretch = value ? Stretch.UniformToFill : Stretch.Uniform;
        if (value == false)
            DisplayPlayTypePolygon = false;
    }

    partial void OnDisplayPlayTypePolygonChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.DisplayPlayTypePolygon, value);
    
    partial void OnDisplayVirtualGameChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.DisplayVirtualGame, value);
    
    partial void OnSpecialDisplayVirtualGameChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.SpecialDisplayVirtualGame, value);
        GameToOpacityConverter.SpecialDisplayVirtualGame = value;
        _galgameService.RefreshDisplay();
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
