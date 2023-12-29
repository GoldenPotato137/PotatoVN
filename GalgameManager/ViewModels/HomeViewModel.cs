using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models.Filters;
using Microsoft.UI.Xaml.Media.Animation;

namespace GalgameManager.ViewModels;

public partial class HomeViewModel : ObservableRecipient, INavigationAware
{
    private const int SearchDelay = 500;
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
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
    public readonly string UiEdit = "HomePage_Edit".GetLocalized();
    public readonly string UiDownLoad = "HomePage_Download".GetLocalized();
    public readonly string UiRemove = "HomePage_Remove".GetLocalized();
    private readonly string _uiSearch = "HomePage_Search_Label".GetLocalized();
    #endregion

    public ICommand ItemClickCommand
    {
        get;
    }

    public ObservableCollection<Galgame> Source { get; private set; } = new();
    public ObservableCollection<FilterBase> Filters = null!;
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly ObservableCollection<FilterBase> FilterInputSuggestions = new();

    public HomeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> dataCollectionService,
        ILocalSettingsService localSettingsService, IFilterService filterService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)_dataCollectionService;
        _localSettingsService = localSettingsService;
        _filterService = filterService;

        ItemClickCommand = new RelayCommand<Galgame>(OnItemClick);
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        SearchKey = _galgameService.GetSearchKey();
        UpdateSearchTitle();
        Source = await _dataCollectionService.GetContentGridDataAsync();
        Filters = _filterService.GetFilters();
        
        Stretch = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture)
            ? Stretch.UniformToFill : Stretch.Uniform;
        FixHorizontalPicture = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture);
        DisplayPlayTypePolygon = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon);
        DisplayVirtualGame = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayVirtualGame);
        SpecialDisplayVirtualGame = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SpecialDisplayVirtualGame);
        KeepFilters = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters);
        GameToOpacityConverter.SpecialDisplayVirtualGame = SpecialDisplayVirtualGame;
        
        Filters.CollectionChanged += UpdateFilterPanelDisplay;
        _galgameService.GalgameLoadedEvent += OnGalgameLoadedEvent;
        _galgameService.PhrasedEvent += OnGalgameServicePhrased;
        _galgameService.SyncProgressChanged += OnSyncChanged;
        _localSettingsService.OnSettingChanged += OnSettingChanged;
        UpdateFilterPanelDisplay(null,null!);
    }

    private void OnSettingChanged(string key, object value)
    {
        switch (key)
        {
            case KeyValues.DisplayVirtualGame:
                DisplayVirtualGame = (bool)value;
                break;
        }
    }

    public async void OnNavigatedFrom()
    {
        await Task.Delay(200); //等待动画结束
        if(await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters) == false)
            _filterService.ClearFilters();
        _galgameService.PhrasedEvent -= OnGalgameServicePhrased;
        _galgameService.SyncProgressChanged -= OnSyncChanged;
        _galgameService.GalgameLoadedEvent -= OnGalgameLoadedEvent;
        Filters.CollectionChanged -= UpdateFilterPanelDisplay;
        _localSettingsService.OnSettingChanged -= OnSettingChanged;
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
    
    #region FILTER
    
    [ObservableProperty] private bool _filterListVisible; //是否显示过滤器列表
    [ObservableProperty] private bool _filterInputVisible; //是否显示过滤器输入框
    [ObservableProperty] private string _filterInputText = string.Empty; //过滤器输入框的文本
    [ObservableProperty] private string _uiFilter = string.Empty; //过滤器在AppBar上的文本
    [ObservableProperty] private bool _keepFilters; //是否保留过滤器
    public TransitionCollection FilterFlyoutTransitions = new();

    private void UpdateFilterPanelDisplay(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UiFilter = "HomePage_Filter".GetLocalized() + (ContainNonVirtualGameFilter() ? " ●" : string.Empty);
        FilterListVisible = Filters.Count > 0;
        if (Filters.Count == 0)
            FilterInputVisible = true;
        //Trick: 在这里设置而不在xaml里面设置是为了防止出现动画播放两次(一次为RepositoryThemeTransition，一次为Implicit.ShowAnimations)
        //用两个动画的原因是因为RepositoryThemeTransition的出现动画只能在控件第一次显示时播放
        if (FilterInputVisible && FilterFlyoutTransitions.Count == 0)
            FilterFlyoutTransitions.Add(new RepositionThemeTransition());
        else if (FilterInputVisible == false)
            FilterFlyoutTransitions.Clear();
    }
    
    [RelayCommand]
    private void DeleteFilter(FilterBase filter)
    {
        _filterService.RemoveFilter(filter);
    }

    [RelayCommand]
    private void SetFilterInputVisible() => FilterInputVisible = !FilterInputVisible;

    [RelayCommand]
    private async Task FilterInputTextChange(AutoSuggestBoxTextChangedEventArgs args)
    {
        if(args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        if (FilterInputText == string.Empty)
        {
            FilterInputSuggestions.Clear();
            return;
        }
        List<FilterBase> result = await _filterService.SearchFilters(FilterInputText);
        FilterInputSuggestions.Clear();
        foreach (FilterBase filter in result)
            FilterInputSuggestions.Add(filter);
    }
    
    [RelayCommand]
    private async Task FilterInputQuerySubmitted(AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is FilterBase filter)
            _filterService.AddFilter(filter);
        else if (string.IsNullOrEmpty(args.QueryText) == false)
        {
            List<FilterBase> result = await _filterService.SearchFilters(args.QueryText);
            if (result.Count > 1)
                _filterService.AddFilter(result[0]);
            else
                _ = DisplayMsgAsync(InfoBarSeverity.Error, "HomePage_Filter_Not_Found".GetLocalized());
        }
        FilterInputText = string.Empty;
    }

    [RelayCommand]
    private void OnFilterFlyoutOpening(object arg)
    {
        FilterInputVisible = false;
        UpdateFilterPanelDisplay(null, null!);
    }
    
    private bool ContainNonVirtualGameFilter()
    {
        return Filters.Count > 0 && Filters.Any(f => f.GetType() != typeof(VirtualGameFilter));
    }
    
    partial void OnKeepFiltersChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.KeepFilters, value);

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
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
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
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "HomePage_Remove_Title".GetLocalized(),
            Content = "HomePage_Remove_Message".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
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
