using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using CommunityToolkit.WinUI.Controls;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Filters;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.WinApp.Base.Models.Filters;

// ReSharper disable CollectionNeverQueried.Global

namespace GalgameManager.ViewModels;

public partial class HomeViewModel : ObservableObject, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly GalgameCollectionService _galgameService;
    private readonly IGalgameSourceCollectionService _sourceService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    private readonly IInfoService _infoService;
    private readonly IBgTaskService _bgTaskService;
    private readonly ICategoryService _categoryService;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Stretch _stretch;
    [ObservableProperty] private bool _fixHorizontalPicture; // 是否修复横向图片（截断为标准的长方形）
    [ObservableProperty] private bool _displayPlayTypePolygon = true; // 是否显示游玩状态的小三角形
    [ObservableProperty] private bool _displayVirtualGame; //是否显示虚拟游戏
    [ObservableProperty] private bool _specialDisplayVirtualGame; //是否特殊显示虚拟游戏（降低透明度）

    #region UI
    public readonly string PlayStatus = "HomePage_PlayStatus".GetLocalized();
    private readonly string _uiSearch = "Search".GetLocalized();
    private readonly string _batchSelectAllLabel = "HomePage_SelectAll".GetLocalized();
    private readonly string _batchSelectNoneLabel = "HomePage_SelectNone".GetLocalized();
    public readonly string BatchDownloadLabel = "HomePage_Download".GetLocalized();
    public readonly string BatchRemoveLabel = "HomePage_Remove".GetLocalized();
    #endregion

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsItemClickEnabled))]
    [NotifyPropertyChangedFor(nameof(CanDragItems))]
    [NotifyPropertyChangedFor(nameof(CanReorderItems))]
    [NotifyPropertyChangedFor(nameof(GridSelectionMode))]
    [NotifyPropertyChangedFor(nameof(IsMultiSelectCheckBoxEnabled))]
    [NotifyPropertyChangedFor(nameof(IsAllSelected))]
    [NotifyPropertyChangedFor(nameof(BatchSelectLabel))]
    private bool _isBatchMode;
    public bool IsItemClickEnabled => !IsBatchMode;
    public bool CanDragItems => !IsBatchMode;
    public bool CanReorderItems => !IsBatchMode;
    public ListViewSelectionMode GridSelectionMode =>
        IsBatchMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    public bool IsMultiSelectCheckBoxEnabled => IsBatchMode;
    public bool IsAllSelected => IsBatchMode && Source.Count > 0 && SelectedGalgamesCount == Source.Count;
    public string BatchSelectLabel => IsAllSelected ? _batchSelectNoneLabel : _batchSelectAllLabel;

    /// <summary>
    /// 一定要有ObservableProperty，不然切换页面后不会更新
    /// </summary>
    [ObservableProperty] private AdvancedCollectionView _source = new(new List<Galgame>(), true);

    private readonly HashSet<Galgame> _selectedGalgames = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BatchChangePlayStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchDownloadInfoCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchRemoveCommand))]
    [NotifyPropertyChangedFor(nameof(HasBatchSelection))]
    [NotifyPropertyChangedFor(nameof(IsAllSelected))]
    [NotifyPropertyChangedFor(nameof(BatchSelectLabel))]
    private int _selectedGalgamesCount;

    public bool HasBatchSelection => SelectedGalgamesCount > 0;


    public HomeViewModel(INavigationService navigationService, IGalgameCollectionService dataCollectionService,
        ILocalSettingsService localSettingsService, IFilterService filterService, IInfoService infoService,
        IBgTaskService bgTaskService, ICategoryService categoryService, IGalgameSourceCollectionService sourceService,
        IMessenger bus)
    {
        _navigationService = navigationService;
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _sourceService = sourceService;
        _localSettingsService = localSettingsService;
        _filterService = filterService;
        _infoService = infoService;
        _bgTaskService = bgTaskService;
        _categoryService = categoryService;
        bus.Register<CategoryGroupChangedArg>(this, HandleCategoryGroupChanged);
    }

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            IsBatchMode = false;
            SearchTitle = SearchKey == string.Empty ? _uiSearch : _uiSearch + " ●";
            Source.Source = _galgameService.Galgames;

            //Read Settings
            Stretch = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture)
                ? Stretch.UniformToFill : Stretch.Uniform;
            FixHorizontalPicture = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture);
            DisplayPlayTypePolygon = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon);
            DisplayVirtualGame = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayVirtualGame);
            SpecialDisplayVirtualGame = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SpecialDisplayVirtualGame);
            KeepFilters = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters);
            HomeFilterShowPlayStatusAndSourcePanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.HomeFilterShowPlayStatusAndSourcePanel);
            HomeFilterShowEnginePanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.HomeFilterShowEnginePanel);
            HomeFilterShowDeveloperPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.HomeFilterShowDeveloperPanel);
            HomeFilterShowTagPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.HomeFilterShowTagPanel);
            HomeFilterShowCategoryPanel = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.HomeFilterShowCategoryPanel);
            GameToOpacityConverter.SpecialDisplayVirtualGame = SpecialDisplayVirtualGame;

            PrimaryKey = (SortKeys)_localSettingsService.ReadSettingAsync<int>(KeyValues.PrimarySortKey).Result;
            IsPrimaryDescending = _localSettingsService.ReadSettingAsync<bool>(KeyValues.PrimarySortDescending).Result;

            SecondaryKey = (SortKeys)_localSettingsService.ReadSettingAsync<int>(KeyValues.SecondarySortKey).Result;
            IsSecondaryDescending = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SecondarySortDescending).Result;
            FilterInit();

            var customOrderList = _localSettingsService
                                 .ReadSettingAsync<List<string>>(KeyValues.CustomSortOrder, true).Result
                                  ?? [];
            ApplySort(customOrderList);

            //Add Event
            _galgameService.GalgameLoadedEvent += OnGalgameLoadedEvent;
            _galgameService.GalgameMutated += UpdateGalgame;
            _localSettingsService.OnSettingChanged += OnSettingChanged;
            _galgameService.Galgames.CollectionChanged += OnGalgamesCollectionChanged;
            _sourceService.OnSourceChanged += HandleSourceChanged;
            _filterService.OnFilterChanged += HandleFilterChanged;
            Source.Filter = g =>
            {
                if (g is Galgame game && _filterService.ApplyFilters(game))
                {
                    return SearchKey.IsNullOrEmpty() || game.ApplySearchKey(SearchKey);
                }

                return false;
            };
            Source.Refresh();
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Oops, something went wrong", e);
        }
    }

    private void OnSettingChanged(string key, object? value)
    {
        switch (key)
        {
            case KeyValues.DisplayVirtualGame:
                DisplayVirtualGame = value is true;
                break;
        }
    }

    public async void OnNavigatedFrom()
    {
        try
        {
            await Task.Delay(200); //等待动画结束
            if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters) == false)
                _filterService.ClearFilters();
            _galgameService.GalgameMutated -= UpdateGalgame;
            _galgameService.GalgameLoadedEvent -= OnGalgameLoadedEvent;
            _galgameService.Galgames.CollectionChanged -= OnGalgamesCollectionChanged;
            _sourceService.OnSourceChanged -= HandleSourceChanged;
            _localSettingsService.OnSettingChanged -= OnSettingChanged;
            _filterService.OnFilterChanged -= HandleFilterChanged;
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Oops, something went wrong", e);
        }
    }

    partial void OnIsBatchModeChanged(bool value)
    {
        if (!value)
        {
            ResetBatchSelection();
        }
    }

    private void OnGalgamesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyBatchSelectionStateChanged();
    }

    private void NotifyBatchSelectionStateChanged()
    {
        if (!IsBatchMode) return;
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(BatchSelectLabel));
    }

    private void ResetBatchSelection()
    {
        _selectedGalgames.Clear();
        SelectedGalgamesCount = 0;
    }

    [RelayCommand]
    private void ItemClick(Galgame? clickedItem)
    {
        if (clickedItem == null) return;
        NavigationHelper.NavigateToGalgamePage(_navigationService, new GalgamePageParameter { Galgame = clickedItem });
    }

    #region DRAG_AND_DROP

    [ObservableProperty] private bool _displayDragArea;

    public async void Grid_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();
            switch (items.Count)
            {
                case <= 0:
                    return;
                // 限制只能拖入一个项目
                case > 1:
                    _infoService.Info(InfoBarSeverity.Error, "HomePage_Drop_TooManyItems".GetLocalized());
                    break;
                default:
                    {
                        // 只处理单个项目
                        IStorageItem storageItem = items[0];
                        if (storageItem is StorageFile file &&
                            (file.FileType.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                             file.FileType.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
                        {
                            var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                            _ = AddGalgameInternal(folder);
                        }
                        else if (storageItem is StorageFolder folder)
                        {
                            _ = AddGalgameInternal(folder.Path);
                        }
                        else
                        {
                            _infoService.Info(InfoBarSeverity.Error, "HomePage_Drop_InvalidItem".GetLocalized());
                        }

                        break;
                    }
            }

            DisplayDragArea = false;
        }
        catch (Exception ex)
        {
            _infoService.DeveloperEvent(e: ex);
        }
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
    [ObservableProperty] private string _uiFilter = string.Empty; //过滤器在AppBar上的文本
    [ObservableProperty] private bool _keepFilters; //是否保留过滤器
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomeFilterPlayStatusAndSourcePanelColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterPlayStatusAndSourcePanelGapColumnWidth))]
    private bool _homeFilterShowPlayStatusAndSourcePanel;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomeFilterEnginePanelColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterPlayStatusAndSourcePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterEnginePanelGapColumnWidth))]
    private bool _homeFilterShowEnginePanel;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomeFilterDeveloperPanelColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterPlayStatusAndSourcePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterEnginePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterDeveloperPanelGapColumnWidth))]
    private bool _homeFilterShowDeveloperPanel;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomeFilterTagPanelColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterPlayStatusAndSourcePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterEnginePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterDeveloperPanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterTagPanelGapColumnWidth))]
    private bool _homeFilterShowTagPanel;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomeFilterCategoryPanelVisible))]
    [NotifyPropertyChangedFor(nameof(HomeFilterCategoryPanelColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterPlayStatusAndSourcePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterEnginePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterDeveloperPanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterTagPanelGapColumnWidth))]
    private bool _homeFilterShowCategoryPanel;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomeFilterCategoryPanelVisible))]
    [NotifyPropertyChangedFor(nameof(HomeFilterCategoryPanelColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterPlayStatusAndSourcePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterEnginePanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterDeveloperPanelGapColumnWidth))]
    [NotifyPropertyChangedFor(nameof(HomeFilterTagPanelGapColumnWidth))]
    private bool _forceShowCategoryPanel; //当前过滤器中有无法在其他面板移除的分类过滤器时，强制显示分类面板
    public bool HomeFilterCategoryPanelVisible => HomeFilterShowCategoryPanel || ForceShowCategoryPanel;
    public GridLength HomeFilterPlayStatusAndSourcePanelColumnWidth => GetHomeFilterPanelColumnWidth(HomeFilterShowPlayStatusAndSourcePanel);
    public GridLength HomeFilterEnginePanelColumnWidth => GetHomeFilterPanelColumnWidth(HomeFilterShowEnginePanel);
    public GridLength HomeFilterDeveloperPanelColumnWidth => GetHomeFilterPanelColumnWidth(HomeFilterShowDeveloperPanel);
    public GridLength HomeFilterTagPanelColumnWidth => GetHomeFilterPanelColumnWidth(HomeFilterShowTagPanel);
    public GridLength HomeFilterCategoryPanelColumnWidth => GetHomeFilterPanelColumnWidth(HomeFilterCategoryPanelVisible);
    public GridLength HomeFilterPlayStatusAndSourcePanelGapColumnWidth =>
        GetHomeFilterPanelGapColumnWidth(HomeFilterShowPlayStatusAndSourcePanel,
            HomeFilterShowEnginePanel || HomeFilterShowDeveloperPanel || HomeFilterShowTagPanel ||
            HomeFilterCategoryPanelVisible);
    public GridLength HomeFilterEnginePanelGapColumnWidth =>
        GetHomeFilterPanelGapColumnWidth(HomeFilterShowEnginePanel,
            HomeFilterShowDeveloperPanel || HomeFilterShowTagPanel || HomeFilterCategoryPanelVisible);
    public GridLength HomeFilterDeveloperPanelGapColumnWidth =>
        GetHomeFilterPanelGapColumnWidth(HomeFilterShowDeveloperPanel,
            HomeFilterShowTagPanel || HomeFilterCategoryPanelVisible);
    public GridLength HomeFilterTagPanelGapColumnWidth =>
        GetHomeFilterPanelGapColumnWidth(HomeFilterShowTagPanel, HomeFilterCategoryPanelVisible);
    private static GridLength GetHomeFilterPanelColumnWidth(bool isVisible) =>
        isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    private static GridLength GetHomeFilterPanelGapColumnWidth(bool hasLeftPanel, bool hasRightPanel) =>
        hasLeftPanel && hasRightPanel ? new GridLength(10) : new GridLength(0);
    [ObservableProperty] private string _filterInputText = string.Empty; //过滤器输入框的文本
    public AdvancedCollectionView TagFilters = [], DeveloperFilters = [], EngineFilters = [], SourceFilters = [], PlayStatusFilters = [],
        CategoryFilters = [];
    private readonly ObservableCollection<HomeViewModelFilter> _tagFiltersC = [], _developerFiltersC = [],
        _engineFiltersC = [], _sourceFiltersC = [], _playStatusFiltersC = [], _categoryFiltersC = [];
    [ObservableProperty] private string _tagFilterSearchKey = string.Empty;
    [ObservableProperty] private string _developerFilterSearchKey = string.Empty;
    [ObservableProperty] private string _engineFilterSearchKey = string.Empty;
    [ObservableProperty] private string _categoryFilterSearchKey = string.Empty;
    [ObservableProperty] private HomeViewModelFilter? _selectedPlayStatus;

    private bool _tagFilterCalc;
    private bool _developerFilterCalc;
    private bool _engineFilterCalc;
    private bool _categoryFilterCalc;
    private bool _sourceFilterCalc;
    private bool _playStatusInit;
    private bool _playStatusSelectedInit;

    private void FilterInit()
    {
        TagFilters.Source = _tagFiltersC;
        DeveloperFilters.Source = _developerFiltersC;
        EngineFilters.Source = _engineFiltersC;
        SourceFilters.Source = _sourceFiltersC;
        PlayStatusFilters.Source = _playStatusFiltersC;
        CategoryFilters.Source = _categoryFiltersC;
        _tagFilterCalc = false;
        _playStatusSelectedInit = false;
        _developerFilterCalc = false;
        _engineFilterCalc = false;
        _categoryFilterCalc = false;
        UpdateCategoryPanelForceVisibility();
        UpdateFilterMsg();
    }

    [RelayCommand]
    private async Task OnFilterFlyoutOpening(object arg)
    {
        try
        {
            if (!_tagFilterCalc)
            {
                Dictionary<string, int> allTags = []; //value为包含该tag的游戏数量
                await Task.Run(() =>
                {
                    foreach (Galgame game in _galgameService.Galgames)
                    foreach (var tag in game.Tags.Value ?? [])
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            allTags.TryAdd(tag, 0);
                            allTags[tag]++;
                        }
                });
                await SetFilterList(TagFilters, allTags, tag => new TagFilter(tag),
                    (filter, tag) => filter is TagFilter t && t.Name == tag);
                TagFilters.Filter = f => f is HomeViewModelFilter filter &&
                                         filter.Title.ContainX(TagFilterSearchKey);
                _tagFilterCalc = true;
            }
            if (!_developerFilterCalc)
            {
                Dictionary<Category, int> allDev = []; //value为包含该开发商(分类)的游戏数量
                foreach (Category dev in _categoryService.DeveloperGroup.Categories)
                {
                    allDev.TryAdd(dev, 0);
                    allDev[dev] = dev.GalgamesX.Count;
                }
                await SetFilterList(DeveloperFilters, allDev, category => new CategoryFilter(category),
                    (filter, category) => filter is CategoryFilter c && c.Category.Id == category.Id);
                DeveloperFilters.Filter = f => f is HomeViewModelFilter filter &&
                                              filter.Title.ContainX(DeveloperFilterSearchKey);
                _developerFilterCalc = true;
            }
            if (!_engineFilterCalc)
            {
                Dictionary<Category, int> allEngines = []; //value为包含该引擎(分类)的游戏数量
                foreach (Category engine in _categoryService.EngineGroup.Categories)
                {
                    allEngines.TryAdd(engine, 0);
                    allEngines[engine] = engine.GalgamesX.Count;
                }
                await SetFilterList(EngineFilters, allEngines, category => new CategoryFilter(category),
                    (filter, category) => filter is CategoryFilter c && c.Category.Id == category.Id);
                EngineFilters.Filter = f => f is HomeViewModelFilter filter &&
                                            filter.Title.ContainX(EngineFilterSearchKey);
                _engineFilterCalc = true;
            }
            if (!_categoryFilterCalc)
            {
                Dictionary<Category, int> allCategories = []; //value为包含该分类的游戏数量
                foreach (Category category in (await _categoryService.GetCategoryGroupsAsync())
                             .Where(group => group.Type == CategoryGroupType.Custom)
                             .SelectMany(group => group.Categories))
                {
                    allCategories.TryAdd(category, 0);
                    allCategories[category] = category.GalgamesX.Count;
                }
                await SetFilterList(CategoryFilters, allCategories, category => new CategoryFilter(category),
                    (filter, category) => filter is CategoryFilter c && c.Category.Id == category.Id);
                CategoryFilters.Filter = f => f is HomeViewModelFilter filter &&
                                              filter.Title.ContainX(CategoryFilterSearchKey);
                _categoryFilterCalc = true;
            }
            if (!_sourceFilterCalc)
            {
                Dictionary<GalgameSourceBase, int> allSource = [];
                foreach (GalgameSourceBase source in _sourceService.GetGalgameSources())
                {
                    allSource.TryAdd(source, 0);
                    allSource[source] = source.GetGalgameList().Count();
                }
                await SetFilterList(SourceFilters, allSource, source => new SourceFilter(source),
                    (filter, source) => filter is SourceFilter s && s.Source == source);
                _sourceFilterCalc = true;
            }
            if (!_playStatusInit)
            {
                CategoryGroup group = _categoryService.StatusGroup;
                Dictionary<Category, int> allStatus = [];
                for(var i = 0; i < group.Categories.Count; i++)
                    allStatus.Add(group.Categories[i], i);
                await SetFilterList(PlayStatusFilters, allStatus, status => new CategoryFilter(status),
                    (filter, status) => filter is CategoryFilter ps && ps.Category.Id == status.Id);
                _playStatusInit = true;
            }
            if (!_playStatusSelectedInit)
            {
                HashSet<Category> playStatusC = new(_categoryService.StatusGroup.Categories);
                CategoryFilter? existStatusCf = _filterService.GetFilters()
                    .FirstOrDefault(f => f is CategoryFilter cf && playStatusC.Contains(cf.Category)) as CategoryFilter;
                if (existStatusCf is not null)
                {
                    foreach (HomeViewModelFilter status in _playStatusFiltersC)
                        if (status.Filter is CategoryFilter cf && cf.Category.Id == existStatusCf.Category.Id)
                        {
                            SelectedPlayStatus = status;
                            break;
                        }
                }
                _playStatusSelectedInit = true;
            }
        }
        catch (Exception e)
        {
            _infoService.Log(msg: e.ToString());
        }
        return;

        async Task SetFilterList<T>(AdvancedCollectionView list, Dictionary<T, int> iter,
            Func<T, FilterBase> makeNewFilter, Func<FilterBase, T, bool> matchFilter) where T : notnull
        {
            list.Clear();
            Dictionary<string, int> clickCount = await _localSettingsService
                .ReadSettingAsync<Dictionary<string, int>>(KeyValues.GameFilterClickCnt) ?? [];
            var maxTagCnt = iter.Count > 0 ? iter.Values.Max() : 0;
            HashSet<FilterBase> activeFilters = new(_filterService.GetFilters());
            foreach (KeyValuePair<T, int> kv in iter)
            {
                FilterBase? existFilter = activeFilters.FirstOrDefault(f => matchFilter(f, kv.Key));
                FilterBase tmp = existFilter ?? makeNewFilter(kv.Key);
                list.Add(new HomeViewModelFilter(tmp,
                    kv.Value + maxTagCnt * clickCount.GetValueOrDefault(tmp.ToString()) * 0.3,
                    _filterService, _localSettingsService, existFilter is null ? false : existFilter.Revert ? null : true));
            }
            list.SortDescriptions.Clear();
            list.SortDescriptions.Add(new SortDescription(nameof(HomeViewModelFilter.SortPoint), SortDirection.Descending));
            list.RefreshSorting();
        }
    }

    [RelayCommand]
    private void SearchTagFilter()
    {
        try
        {
            TagFilters.RefreshFilter();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    [RelayCommand]
    private void SearchDeveloperFilter()
    {
        try
        {
            DeveloperFilters.RefreshFilter();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    [RelayCommand]
    private void SearchEngineFilter()
    {
        try
        {
            EngineFilters.RefreshFilter();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    [RelayCommand]
    private void SearchCategoryFilter()
    {
        try
        {
            CategoryFilters.RefreshFilter();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    [RelayCommand]
    private void ClearPlayStatus() => SelectedPlayStatus = null;

    [RelayCommand]
    private void ClearAllFilters()
    {
        SelectedPlayStatus = null;
        ClearFilterSelection(TagFilters);
        ClearFilterSelection(DeveloperFilters);
        ClearFilterSelection(EngineFilters);
        ClearFilterSelection(SourceFilters);
        ClearFilterSelection(CategoryFilters);
        return;

        static void ClearFilterSelection(AdvancedCollectionView filters)
        {
            foreach (HomeViewModelFilter filter in filters.OfType<HomeViewModelFilter>())
                filter.IsChecked = false;
        }
    }

    partial void OnKeepFiltersChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.KeepFilters, value);

    [RelayCommand]
    private async Task ShowHomeFilterPanelVisibilityDialog()
    {
        HomeFilterPanelVisibilityDialog dialog = new(HomeFilterShowPlayStatusAndSourcePanel, HomeFilterShowEnginePanel,
            HomeFilterShowDeveloperPanel, HomeFilterShowTagPanel, HomeFilterShowCategoryPanel);
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        HomeFilterShowPlayStatusAndSourcePanel = dialog.ShowPlayStatusAndSourcePanel;
        HomeFilterShowEnginePanel = dialog.ShowEnginePanel;
        HomeFilterShowDeveloperPanel = dialog.ShowDeveloperPanel;
        HomeFilterShowTagPanel = dialog.ShowTagPanel;
        HomeFilterShowCategoryPanel = dialog.ShowCategoryPanel;

        await _localSettingsService.SaveSettingAsync(KeyValues.HomeFilterShowPlayStatusAndSourcePanel,
            HomeFilterShowPlayStatusAndSourcePanel);
        await _localSettingsService.SaveSettingAsync(KeyValues.HomeFilterShowEnginePanel, HomeFilterShowEnginePanel);
        await _localSettingsService.SaveSettingAsync(KeyValues.HomeFilterShowDeveloperPanel, HomeFilterShowDeveloperPanel);
        await _localSettingsService.SaveSettingAsync(KeyValues.HomeFilterShowTagPanel, HomeFilterShowTagPanel);
        await _localSettingsService.SaveSettingAsync(KeyValues.HomeFilterShowCategoryPanel, HomeFilterShowCategoryPanel);
    }

    private void HandleFilterChanged()
    {
        UpdateFilterMsg();
        UpdateCategoryPanelForceVisibility();
        Source.Refresh();
    }

    private void UpdateFilterMsg()
    {
        ObservableCollection<FilterBase> filters = _filterService.GetFilters();
        var hasActiveFilters = filters.Count > 0 && (DisplayVirtualGame || filters.Any(f => !(f is VirtualGameFilter)));
        UiFilter = "HomePage_Filter".GetLocalized() + (hasActiveFilters ? " ●" : string.Empty);
    }

    private void HandleCategoryGroupChanged(object recipient, CategoryGroupChangedArg message)
    {
        if (message.Group.Type == CategoryGroupType.Developer)
            _developerFilterCalc = false;
        else if (message.Group.Type == CategoryGroupType.Engine)
            _engineFilterCalc = false;
        else if (message.Group.Type == CategoryGroupType.Custom)
            _categoryFilterCalc = false;
        UpdateCategoryPanelForceVisibility();
    }

    /// <summary>
    /// 若当前过滤器中有不属于游玩状态/开发商/引擎组的分类过滤器（在其他面板中无法移除），则强制显示分类过滤面板
    /// </summary>
    private void UpdateCategoryPanelForceVisibility()
    {
        ForceShowCategoryPanel = _filterService.GetFilters()
            .Any(f => f is CategoryFilter cf && !IsBuiltInCategory(cf.Category));
    }

    private bool IsBuiltInCategory(Category category) =>
        _categoryService.StatusGroup.Categories.Any(c => c.Id == category.Id) ||
        _categoryService.DeveloperGroup.Categories.Any(c => c.Id == category.Id) ||
        _categoryService.EngineGroup.Categories.Any(c => c.Id == category.Id);

    private void HandleSourceChanged() => _sourceFilterCalc = false;

    partial void OnSelectedPlayStatusChanged(HomeViewModelFilter? value)
    {
        HashSet<Category> playStatus = new(_categoryService.StatusGroup.Categories);
        FilterBase? existFilter = _filterService.GetFilters()
                .FirstOrDefault(f => f is CategoryFilter c && playStatus.Contains(c.Category));
        if ((existFilter as CategoryFilter) is { } existStatusCf && value?.Filter is CategoryFilter newStatusCf
                                                                 && existStatusCf.Category.Id == newStatusCf.Category.Id)
            return; //如果选择了相同的状态则不做任何操作
        if (existFilter is not null) _filterService.RemoveFilter(existFilter);
        if (value?.Filter is not CategoryFilter) return;
        _filterService.AddFilter(value.Filter);
    }

    #endregion

    #region SEARCH
    [ObservableProperty] private string _searchKey = string.Empty;
    [ObservableProperty] private string _searchTitle = string.Empty;
    [ObservableProperty]
    private GalgameSearchSuggestionsProvider _galgameSearchSuggestionsProvider = new();

    [RelayCommand]
    private void Search(string searchKey)
    {
        SearchTitle = searchKey == string.Empty ? _uiSearch : _uiSearch + " ●";
        RefreshFilterAndSelection();
    }

    private void RefreshFilterAndSelection()
    {
        Source.RefreshFilter();
        NotifyBatchSelectionStateChanged();
    }

    #endregion

    #region BATCH_MANAGE

    [RelayCommand]
    private void ToggleBatchManageState() => IsBatchMode = !IsBatchMode;

    [RelayCommand]
    private void MainGridViewSelectionChanged(SelectionChangedEventArgs args)
    {
        if (!IsBatchMode) return;
        foreach (Galgame game in args.AddedItems.OfType<Galgame>())
            _selectedGalgames.Add(game);
        foreach (Galgame game in args.RemovedItems.OfType<Galgame>())
            _selectedGalgames.Remove(game);
        SelectedGalgamesCount = _selectedGalgames.Count;
    }

    [RelayCommand(CanExecute = nameof(HasBatchSelection))]
    private async Task BatchChangePlayStatus(string playTypeString)
    {
        if (!Enum.TryParse(playTypeString, out PlayType playType)) return;
        List<Galgame> selectedGalgames = GetSelectedGalgamesInViewOrder();
        if (selectedGalgames.Count == 0) return;

        var title = $"{PlayStatus} - {playType.GetLocalized()}";
        BatchChangePlayStatusDialog dialog = new(selectedGalgames, title, playType);
        dialog.Resources["ContentDialogMinWidth"] = App.MainWindow!.Bounds.Width * 0.4;
        await dialog.ShowAsync();
        if (dialog.Canceled) return;

        foreach (Galgame game in selectedGalgames)
        {
            game.PlayType = PlayType.None; //确保分类事件能被触发
            game.PlayType = dialog.SelectedPlayType;
            game.MyRate = dialog.SelectedRate;
            game.PrivateComment = dialog.PrivateComment;

            if (dialog.UploadToBgm)
            {
                _infoService.Info(InfoBarSeverity.Informational,
                    msg: "HomePage_UploadingToBgm".GetLocalized(),
                    displayTimeMs: 1000 * 10);
                (GalStatusSyncResult, string) result = await _galgameService.UploadPlayStatusAsync(game, RssType.Bangumi);
                _infoService.Info(result.Item1.ToInfoBarSeverity(), result.Item2);
                await Task.Delay(Random.Shared.Next(300, 801));
            }

            if (dialog.UploadToVndb)
            {
                _infoService.Info(InfoBarSeverity.Informational,
                    msg: "HomePage_UploadingToVndb".GetLocalized(),
                    displayTimeMs: 1000 * 10);
                (GalStatusSyncResult, string) result = await _galgameService.UploadPlayStatusAsync(game, RssType.Vndb);
                _infoService.Info(result.Item1.ToInfoBarSeverity(), result.Item2);
                await Task.Delay(Random.Shared.Next(300, 801));
            }

            await _galgameService.SaveGalgameAsync(game);
        }
    }

    private static readonly List<GetGalgameInfoFromRssTask> BatchRssTasks = [];

    [RelayCommand(CanExecute = nameof(HasBatchSelection))]
    private async Task BatchDownloadInfo()
    {
        if (_selectedGalgames.Count == 0) return;
        if (!await ConfirmBatchActionAsync(BatchDownloadLabel)) return;

        SelectGameInfoToFetchDialog selectInfoDialog = new(showIncludingSubSourcesCheckBox: false);
        await selectInfoDialog.ShowAsync();
        if (selectInfoDialog.Canceled) return;

        GameParseType selectedParseTypes = selectInfoDialog.SelectedParseTypes;
        var groups = _selectedGalgames
            .Select(game => new
            {
                Game = game,
                Source = game.PreferredLocalInstallation?.Source
                         ?? game.SourceEntries.Select(e => e.Source)
                             .FirstOrDefault(s => s?.SourceType == GalgameSourceType.Virtual)
            })
            .Where(item => item.Source is not null)
            .GroupBy(item => item.Source!);

        foreach (var group in groups)
        {
            var task = new GetGalgameInfoFromRssTask(group.Key, selectedParseTypes,
                group.Select(item => item.Game).ToList());
            BatchRssTasks.Add(task);
            _ = _bgTaskService.AddBgTask(task);
        }
    }

    [RelayCommand(CanExecute = nameof(HasBatchSelection))]
    private async Task BatchRemove()
    {
        if (_selectedGalgames.Count == 0) return;
        var title = "HomePage_Remove_Title".GetLocalized();
        var message = "HomePage_BatchRemove_Message".GetLocalized(_selectedGalgames.Count);
        if (!await ConfirmBatchActionAsync(title, message)) return;

        foreach (Galgame game in _selectedGalgames.ToList())
        {
            await _galgameService.RemoveGalgame(game);
        }
        ResetBatchSelection();
    }

    #endregion

    #region SORT

    // 为XAML绑定添加静态排序键属性
    [ObservableProperty] private SortKeys _primaryKey = SortKeys.LastPlay;
    [ObservableProperty] private bool _isPrimaryDescending;
    [ObservableProperty] private SortKeys _secondaryKey = SortKeys.Developer;
    [ObservableProperty] private bool _isSecondaryDescending;

    private bool _suppressSort;

    [RelayCommand]
    private void SetPrimaryKey(string key)
    {
        if (Enum.TryParse(key, out SortKeys sortKey))
        {
            PrimaryKey = sortKey;
            // 离开手动排序时，把遗留的次排序键（可能是Custom）重置为一个有效的键
            if (sortKey != SortKeys.Custom && SecondaryKey == SortKeys.Custom)
            {
                SecondaryKey = SortKeys.Name;
                IsSecondaryDescending = false;
            }
            ApplySort();
        }
    }

    [RelayCommand]
    private void SetSecondaryKey(string key)
    {
        if (Enum.TryParse(key, out SortKeys sortKey))
        {
            SecondaryKey = sortKey;
            ApplySort();
        }
    }

    /// <summary>
    /// 拖动开始时切换到手动排序模式。先把当前排序（含被过滤掉的游戏）固化到集合的物理顺序，
    /// 再清空排序描述，这样列表在开始拖动时不会突然跳变成集合的原始顺序。
    /// </summary>
    public void EnterCustomSortMode()
    {
        if (PrimaryKey == SortKeys.Custom) return;
        _suppressSort = true;
        try
        {
            MaterializeSortOrderToCollection();
            PrimaryKey = SortKeys.Custom;
            IsPrimaryDescending = false;
            Source.SortDescriptions.Clear();
            Source.RefreshSorting();
            SaveSortSettings();
        }
        finally
        {
            _suppressSort = false;
        }
    }

    /// <summary>
    /// 按当前的排序描述（包含被过滤器隐藏的游戏）把 <see cref="_galgameService"/> 的
    /// 底层集合物理重排一次，用于从其它排序方式切换到手动排序时避免视觉跳变。
    /// </summary>
    private void MaterializeSortOrderToCollection()
    {
        if (Source.SortDescriptions.Count == 0) return;
        AdvancedCollectionView sorted = new(_galgameService.Galgames.ToList(), false);
        foreach (SortDescription sd in Source.SortDescriptions)
            sorted.SortDescriptions.Add(sd);
        sorted.RefreshSorting();
        ReorderCollectionTo(sorted.Cast<Galgame>().ToList());
    }

    private void ApplyCustomOrder(List<string> customOrder)
    {
        Source.SortDescriptions.Clear();
        Source.RefreshSorting();
        var indexMap = customOrder
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        ObservableCollection<Galgame> collection = _galgameService.Galgames;

        List<Galgame> inOrderItems = new();
        List<Galgame> missingItems = new();
        foreach (Galgame gal in collection)
        {
            if (indexMap.ContainsKey(gal.Uuid.ToString()))
                inOrderItems.Add(gal);
            else
                missingItems.Add(gal); //未记录顺序的游戏（例如新添加的）排在末尾
        }
        inOrderItems
            .Sort((a, b) =>
                indexMap[a.Uuid.ToString()].CompareTo(indexMap[b.Uuid.ToString()]));
        ReorderCollectionTo(inOrderItems.Concat(missingItems).ToList());
    }

    /// <summary>
    /// 把底层集合原地重排为 <paramref name="target"/> 指定的顺序。
    /// </summary>
    private void ReorderCollectionTo(List<Galgame> target)
    {
        ObservableCollection<Galgame> collection = _galgameService.Galgames;
        for (var i = 0; i < target.Count && i < collection.Count; i++)
        {
            var oldIndex = collection.IndexOf(target[i]);
            if (oldIndex != i && oldIndex != -1)
                collection.Move(oldIndex, i);
        }
    }

    [RelayCommand]
    private void ApplySort(List<string>? customOrder = null)
    {
        if (_suppressSort) return;
        SaveSortSettings();

        // 清除现有排序
        Source.SortDescriptions.Clear();

        // 手动排序：只由主排序键决定，忽略次排序键
        if (PrimaryKey == SortKeys.Custom)
        {
            Source.RefreshSorting();
            // 从菜单切换到手动排序时不会传入顺序，此处从设置中读取已保存的顺序
            customOrder ??= _localSettingsService
                .ReadSettingAsync<List<string>>(KeyValues.CustomSortOrder, true).Result;
            if (customOrder is { Count: > 0 }) ApplyCustomOrder(customOrder);
            return;
        }

        // 应用主排序键
        SortDirection primaryDirection = IsPrimaryDescending ? SortDirection.Descending : SortDirection.Ascending;
        switch (PrimaryKey)
        {
            case SortKeys.Name:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Name),
                    primaryDirection, StringComparer.CurrentCultureIgnoreCase));
                break;
            case SortKeys.Developer:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Developer),
                    primaryDirection, StringComparer.Ordinal));
                break;
            case SortKeys.Rating:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Rating),
                    primaryDirection));
                break;
            case SortKeys.LastPlay:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastPlayTime),
                    primaryDirection));
                break;
            case SortKeys.ReleaseDate:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.ReleaseDate),
                    primaryDirection));
                break;
            case SortKeys.LastFetchInfoTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastFetchInfoTime),
                    primaryDirection));
                break;
            case SortKeys.AddTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.AddTime),
                    primaryDirection));
                break;
        }

        // 应用次要排序键
        SortDirection secondaryDirection = IsSecondaryDescending ? SortDirection.Descending : SortDirection.Ascending;
        switch (SecondaryKey)
        {
            case SortKeys.Name:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Name),
                    secondaryDirection, StringComparer.CurrentCultureIgnoreCase));
                break;
            case SortKeys.Developer:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Developer),
                    secondaryDirection, StringComparer.Ordinal));
                break;
            case SortKeys.Rating:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Rating),
                    secondaryDirection));
                break;
            case SortKeys.LastPlay:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastPlayTime),
                    secondaryDirection));
                break;
            case SortKeys.ReleaseDate:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.ReleaseDate),
                    secondaryDirection));
                break;
            case SortKeys.LastFetchInfoTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastFetchInfoTime),
                    secondaryDirection));
                break;
            case SortKeys.AddTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.AddTime),
                    secondaryDirection));
                break;
        }

        Source.RefreshSorting();
    }

    // 新增方法，统一保存排序相关设置
    private void SaveSortSettings()
    {
        _localSettingsService.SaveSettingAsync(KeyValues.PrimarySortKey, (int)PrimaryKey);
        _localSettingsService.SaveSettingAsync(KeyValues.PrimarySortDescending, IsPrimaryDescending);
        _localSettingsService.SaveSettingAsync(KeyValues.SecondarySortKey, (int)SecondaryKey);
        _localSettingsService.SaveSettingAsync(KeyValues.SecondarySortDescending, IsSecondaryDescending);
    }

    public async void SaveCustomSortOrder()
    {
        try
        {
            // 保存整个游戏库的顺序而不是过滤后的视图，避免过滤/搜索状态下丢失被隐藏游戏的顺序
            List<string> customSortOrder = _galgameService.Galgames
                .Select(g => g.Uuid.ToString())
                .ToList();
            await _localSettingsService.SaveSettingAsync(KeyValues.CustomSortOrder, customSortOrder, true);
        }
        catch (Exception ex)
        {
            _infoService.DeveloperEvent(e: ex);
        }
    }

    #endregion

    /// <summary>
    /// 添加Galgame
    /// </summary>
    /// <param name="path">游戏文件夹路径</param>
    /// <param name="isVirtual">添加非本机游戏则设为true</param>
    private async Task AddGalgameInternal(string path, bool isVirtual = false)
    {
        //TODO
        IsPhrasing = true;
        InfoBarSeverity infoBarSeverity;
        string msg;
        try
        {
            Galgame tmp = await _galgameService.AddGameAsync(
                isVirtual ? GalgameSourceType.Virtual : GalgameSourceType.LocalFolder, path, true);
            infoBarSeverity = tmp.IsIdsEmpty() ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
            msg = tmp.IsIdsEmpty()
                ? "AddGalgameResult_NotFoundInRss".GetLocalized()
                : "AddGalgameResult_Success".GetLocalized();
        }
        catch (Exception e)
        {
            infoBarSeverity = InfoBarSeverity.Error;
            msg = e is PvnException ? e.Message : e.ToString();
        }

        IsPhrasing = false;
        _infoService.Info(infoBarSeverity, msg: msg);
    }

    // private void OnGalgameLoadedEvent() => Source.Source = _galgameService.Galgames;
    private void OnGalgameLoadedEvent()
    {
        Source.Source = _galgameService.Galgames;
        Source.Refresh();
        NotifyBatchSelectionStateChanged();
    }

    private void UpdateGalgame(object? sender, GalgameMutationEventArgs args)
    {
        _tagFilterCalc = false;
        if (args.Changes.HasFlag(GalgameChangeKind.Added)) return;
        //通过Remove和Add来刷新某个具体的Item
        UiThreadInvokeHelper.Invoke(() =>
        {
            Source.Remove(args.Game);
            Source.Add(args.Game);
        });
    }

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
            _infoService.Info(InfoBarSeverity.Error, msg: e.ToString());
        }
    }

    [RelayCommand]
    private async Task AddVirtualGame()
    {
        BasicDialog dialog = new("GalgamePage_AddVirtualGame".GetLocalized(), inputBox: true,
            inputBoxPlaceHolder: "GalgamePage_AddVirtualGame_PlaceHolder".GetLocalized(), minWidth: 200);
        await dialog.ShowAsync();
        if (!dialog.PrimaryButtonClicked) return;
        await AddGalgameInternal(dialog.InputText, true);
    }

    #region MenuFlyout
    [ObservableProperty] private Galgame? _currentContextGame; // 当前右键菜单上下文游戏对象

    [RelayCommand]
    private async Task GalFlyOutDelete(Galgame? galgame)
    {
        if (galgame == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "HomePage_Remove_Title".GetLocalized(),
            Content = "HomePage_Remove_Message".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(galgame);
        };

        await dialog.ShowAsync();
    }

    [RelayCommand]
    private void GalFlyOutEdit(Galgame? galgame)
    {
        if (galgame == null) return;
        // 在主线程中执行导航，修复appbarbutton描述文字延迟显示的问题
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
            _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame)
        );
    }

    [RelayCommand]
    private async Task GalFlyOutGetInfoFromRss(Galgame? galgame)
    {
        if (galgame == null) return;
        IsPhrasing = true;
        try
        {
            await _galgameService.ParseGalInfoAsync(galgame);
        }
        finally
        {
            IsPhrasing = false;
        }
    }

    [RelayCommand]
    private void SetCurrentContextGame(Galgame? game)
    {
        CurrentContextGame = game;
    }

    [RelayCommand]
    private async Task GalFlyOutChangePlayStatus(string playTypeString)
    {
        if (CurrentContextGame == null) return;

        if (!Enum.TryParse(playTypeString, out PlayType playType))
            return;

        CurrentContextGame.PlayType = playType;

        await _galgameService.SaveGalgameAsync(CurrentContextGame);
    }

    public bool IsCurrentPlayType(Galgame? game, string playTypeString)
    {
        if (game == null) return false;

        if (!Enum.TryParse(playTypeString, out PlayType playType))
            return false;

        return game.PlayType == playType;
    }

    [RelayCommand]
    private async Task ShowChangePlayStatusDialog(Galgame? game)
    {
        if (game == null) return;

        ChangePlayStatusDialog dialog = new(game);
        await dialog.ShowAsync();

        if (!dialog.Canceled)
        {
            await _galgameService.SaveGalgameAsync(game);
        }
    }

    public void GalFlyout_Opening(object sender, object e)
    {
        if (sender is MenuFlyout flyout && flyout.Target != null)
        {
            Galgame? game = flyout.Target.DataContext as Galgame;
            SetCurrentContextGame(game);
            MenuFlyoutSubItem? folders = flyout.Items.OfType<MenuFlyoutSubItem>()
                .FirstOrDefault(item => item.Tag as string == "InstallationFolders");
            if (folders is not null)
            {
                folders.Items.Clear();
                foreach (GalgameAndPath installation in game?.LocalInstallations ?? [])
                {
                    folders.Items.Add(new MenuFlyoutItem
                    {
                        Text = installation.DisplayName,
                        Command = OpenInstallationInExplorerCommand,
                        CommandParameter = installation,
                    });
                }
                folders.IsEnabled = folders.Items.Count > 0;
            }
        }
    }

    [RelayCommand]
    private async Task OpenGameInExplorer(Galgame? game)
    {
        await OpenInstallationInExplorer(game?.PreferredLocalInstallation);
    }

    [RelayCommand]
    private static async Task OpenInstallationInExplorer(GalgameAndPath? installation)
    {
        if (installation is null || !Directory.Exists(installation.Path)) return;
        StorageFolder? folder = await StorageFolder.GetFolderFromPathAsync(installation.Path);
        await Launcher.LaunchFolderAsync(folder);
    }

    #endregion

    private async Task<bool> ConfirmBatchActionAsync(string title, string? message = null)
    {
        if (_selectedGalgames.Count == 0) return false;

        List<Galgame> selectedGalgames = GetSelectedGalgamesInViewOrder();
        BatchConfirmDialog dialog = new(selectedGalgames, title, message)
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot
        };
        dialog.Resources["ContentDialogMinWidth"] = App.MainWindow.Bounds.Width * 0.4;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private List<Galgame> GetSelectedGalgamesInViewOrder()
    {
        List<Galgame> selectedGalgames = [.. Source.OfType<Galgame>().Where(game => _selectedGalgames.Contains(game))];
        return selectedGalgames.Count > 0 ? selectedGalgames : [.. _selectedGalgames];
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
        foreach (Galgame game in _galgameService.Galgames)
            game.RaisePropertyChanged(nameof(Galgame.IsLocalGame));
    }
}

public class GalgameSearchSuggestionsProvider : ISearchSuggestionsProvider
{
    private readonly GalgameCollectionService _galgameCollectionService;
    private readonly bool _searchName, _searchDeveloper, _searchTags, _searchChineseName, _searchOriginalName;

    public GalgameSearchSuggestionsProvider(bool searchName = true, bool searchDeveloper = true, bool searchTags = true, bool searchChineseName = true, bool searchOriginalName = true)
    {
        _searchName = searchName;
        _searchDeveloper = searchDeveloper;
        _searchTags = searchTags;
        _searchChineseName = searchChineseName;
        _searchOriginalName = searchOriginalName;
        _galgameCollectionService = (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!;
    }
    public async Task<IEnumerable<string>?> GetSearchSuggestionsAsync(string key)
    {
        return await _galgameCollectionService.GetSearchSuggestions(key, _searchName, _searchDeveloper, _searchTags, _searchChineseName, _searchOriginalName);
    }
}

public partial class HomeViewModelFilter : ObservableRecipient
{
    public FilterBase Filter { get; }
    [ObservableProperty] private bool? _isChecked = false;
    private readonly double _baseSortPoint;
    private readonly IFilterService _filterService;
    private readonly ILocalSettingsService _settingsService;

    public HomeViewModelFilter(FilterBase filter, double baseSortPoint, IFilterService filterService,
        ILocalSettingsService settingsService, bool? initialState)
    {
        Filter = filter;
        _baseSortPoint = baseSortPoint;
        _filterService = filterService;
        _settingsService = settingsService;
        IsChecked = initialState;
    }

    public string Title => Filter.Name;
    public double SortPoint => _baseSortPoint + (IsChecked is true ? 1 : 0) * 1e7;

    partial void OnIsCheckedChanged(bool? value)
    {
        if (value is true)
        {
            Filter.Revert = false;
            _filterService.AddFilter(Filter);
            Dictionary<string, int> cntMap =
                _settingsService.ReadSettingAsync<Dictionary<string, int>>(KeyValues.GameFilterClickCnt).Result ?? [];
            var cnt = cntMap.GetValueOrDefault(Filter.ToString()) + 1;
            cntMap[Filter.ToString()] = cnt;
            _settingsService.SaveSettingAsync(KeyValues.GameFilterClickCnt, cntMap);
        }
        else if (value is false) _filterService.RemoveFilter(Filter);
        else
        {
            Filter.Revert = true;
            _filterService.SetFilter(Filter);
        }
    }

    public override string ToString() => Title;
}
