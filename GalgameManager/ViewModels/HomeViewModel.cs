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
// ReSharper disable CollectionNeverQueried.Global

namespace GalgameManager.ViewModels;

public partial class HomeViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    private readonly IInfoService _infoService;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Stretch _stretch;
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

    public HomeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> dataCollectionService,
        ILocalSettingsService localSettingsService, IFilterService filterService, IInfoService infoService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)_dataCollectionService;
        _localSettingsService = localSettingsService;
        _filterService = filterService;
        _infoService = infoService;

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
        _localSettingsService.OnSettingChanged += OnSettingChanged;
        UpdateFilterPanelDisplay(null,null!);
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
        await Task.Delay(200); //等待动画结束
        if(await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters) == false)
            _filterService.ClearFilters();
        _galgameService.PhrasedEvent -= OnGalgameServicePhrased;
        _galgameService.GalgameLoadedEvent -= OnGalgameLoadedEvent;
        Filters.CollectionChanged -= UpdateFilterPanelDisplay;
        _localSettingsService.OnSettingChanged -= OnSettingChanged;
    }

    private void OnItemClick(Galgame? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, new GalgamePageParameter {Galgame = clickedItem});
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
    public ObservableCollection<FilterBase> Filters = null!;
    public readonly ObservableCollection<FilterBase> FilterInputSuggestions = new();

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
                _infoService.Info(InfoBarSeverity.Error, msg: "HomePage_Filter_Not_Found".GetLocalized());
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

    #region SEARCH
    
    private const int SearchDelay = 500;
    
    public readonly ObservableCollection<string> SearchSuggestions = new();
    private DateTime _lastSearchTime = DateTime.Now;
    [ObservableProperty] private string _searchKey = string.Empty;
    [ObservableProperty] private string _searchTitle = string.Empty;

    private void UpdateSearchTitle()
    {
        SearchTitle = SearchKey == string.Empty ? _uiSearch : _uiSearch + " ●";
    }
    
    [RelayCommand]
    private async Task SearchChange(AutoSuggestBoxTextChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(SearchKey))
        {
            UpdateSearchTitle();
            _galgameService.Search(string.Empty);
            SearchSuggestions.Clear();
            return;
        }
        
        _ = Task.Run((async Task() =>
        {
            _lastSearchTime = DateTime.Now;
            DateTime tmp = _lastSearchTime;
            await Task.Delay(SearchDelay);
            if (tmp == _lastSearchTime) //如果在延迟时间内没有再次输入，则开始搜索
            {
                await UiThreadInvokeHelper.InvokeAsync(() =>
                {
                    _galgameService.Search(SearchKey);
                    UpdateSearchTitle();
                });
            }
        })!);
        //更新建议
        if(args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        if (SearchKey == string.Empty)
        {
            SearchSuggestions.Clear();
            return;
        }
        List<string> result = await _galgameService.GetSearchSuggestions(SearchKey);
        SearchSuggestions.Clear();
        foreach (var suggestion in result)
            SearchSuggestions.Add(suggestion);
    }
    
    [RelayCommand]
    private void SearchSubmitted(AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (string.IsNullOrEmpty(SearchKey)) return;
        _galgameService.Search(SearchKey);
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
        _infoService.Info(result.ToInfoBarSeverity(), msg: msg);
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
            _infoService.Info(InfoBarSeverity.Error, msg: e.Message);
        }
    }
    
    [RelayCommand]
    private async Task Sort()
    
    {
        await _galgameService.SetSortKeysAsync();
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
}
