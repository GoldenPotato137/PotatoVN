using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Controls;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models.Filters;
using GalgameManager.Models.Sources;

// ReSharper disable CollectionNeverQueried.Global

namespace GalgameManager.ViewModels;

public partial class HomeViewModel : ObservableRecipient, INavigationAware, IRecipient<HomePageSetFilterMessage>
{
    private readonly INavigationService _navigationService;
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInfoService _infoService;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Stretch _stretch;
    [ObservableProperty] private bool _fixHorizontalPicture; // 是否修复横向图片（截断为标准的长方形）
    [ObservableProperty] private bool _displayPlayTypePolygon = true; // 是否显示游玩状态的小三角形
    [ObservableProperty] private bool _displayVirtualGame; //是否显示虚拟游戏
    [ObservableProperty] private bool _specialDisplayVirtualGame; //是否特殊显示虚拟游戏（降低透明度）

    #region UI
    private readonly string _uiSearch = "Search".GetLocalized();
    #endregion
    
    /// <summary>
    /// 一定要有ObservableProperty，不然切换页面后不会更新
    /// </summary>
    [ObservableProperty]
    private AdvancedCollectionView _source = new(null, true);

    public HomeViewModel(INavigationService navigationService, IGalgameCollectionService dataCollectionService,
        ILocalSettingsService localSettingsService, IInfoService infoService)
    {
        _navigationService = navigationService;
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _localSettingsService = localSettingsService;
        _infoService = infoService;
    }
    
    public async void OnNavigatedTo(object parameter)
    {
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
        GameToOpacityConverter.SpecialDisplayVirtualGame = SpecialDisplayVirtualGame;
        SortKeys[] sortKeysList = _localSettingsService.ReadSettingAsync<SortKeys[]>(KeyValues.SortKeys).Result ?? new[]
            { SortKeys.LastPlay , SortKeys.Developer};
        var sortKeysAscending = _localSettingsService.ReadSettingAsync<bool[]>(KeyValues.SortKeysAscending).Result ?? new[]
            {false,false};
        UpdateSortKeys(sortKeysList, sortKeysAscending);
        
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayVirtualGame))
        {
            if (Filters.FirstOrDefault(filter => filter.GetType() == typeof(VirtualGameFilter)) is { } f)
            {
                Filters.Remove(f);
            }
        }
        else
        {

            if (Filters.All(filter => filter.GetType() != typeof(VirtualGameFilter)))
            {
                Filters.Add(new VirtualGameFilter());
            }
        }
        
        //Add Event
        Filters.CollectionChanged += UpdateFilterPanelDisplay;
        _galgameService.GalgameLoadedEvent += OnGalgameLoadedEvent;
        _galgameService.GalgameDeletedEvent += OnGalgameDeletedEvent;
        _galgameService.GalgameAddedEvent += OnGalgameAddedEvent;
        _galgameService.PhrasedEvent += OnGalgameServicePhrased;
        _localSettingsService.OnSettingChanged += OnSettingChanged;
        Source.Filter = g =>
        {
            if (g is Galgame game && Filters.All(filter => filter.Apply(game)))
            {
                return SearchKey.IsNullOrEmpty() || game.ApplySearchKey(SearchKey);
            }

            return false;
        };
        Source.Refresh();
        UpdateFilterPanelDisplay(null,null!);
        IsActive = true;
    }

    private void OnGalgameAddedEvent(Galgame obj)
    {
        if (!Source.Contains(obj))Source.Add(obj);
    }

    private void OnGalgameDeletedEvent(Galgame obj)
    {
        if (Source.Contains(obj)) Source.Remove(obj);

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
        Source.Filter = null;
        if(await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters) == false) 
            Filters.Clear();
        _galgameService.PhrasedEvent -= OnGalgameServicePhrased;
        _galgameService.GalgameLoadedEvent -= OnGalgameLoadedEvent;
        Filters.CollectionChanged -= UpdateFilterPanelDisplay;
        _localSettingsService.OnSettingChanged -= OnSettingChanged;
    }

    [RelayCommand]
    private void ItemClick(Galgame? clickedItem)
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
    [ObservableProperty] private string _uiFilter = string.Empty; //过滤器在AppBar上的文本
    [ObservableProperty] private bool _keepFilters; //是否保留过滤器
    [ObservableProperty] private string _filterInputText = string.Empty; //过滤器输入框的文本
    public ObservableCollection<FilterBase> Filters = new();
    public readonly ObservableCollection<FilterBase> FilterInputSuggestions = new();

    private void UpdateFilterPanelDisplay(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UiFilter = "HomePage_Filter".GetLocalized() + (ContainNonVirtualGameFilter() ? " ●" : string.Empty);
        Source.RefreshFilter();
    }
    
    [RelayCommand]
    private void FilterRemoved(object args)
    {
        if (args is FilterBase filter && Filters.Contains(filter))
        {
            Filters.Remove(filter);
            if (filter is VirtualGameFilter)
                _localSettingsService.SaveSettingAsync(KeyValues.DisplayVirtualGame, true);
        }
    }

    [RelayCommand]
    private async Task FilterInputTextChange(AutoSuggestBoxTextChangedEventArgs args)
    {
        if(args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        if (FilterInputText == string.Empty)
        {
            FilterInputSuggestions.Clear();
            return;
        }
        List<FilterBase> result = await SearchFilters(FilterInputText);
        FilterInputSuggestions.Clear();
        foreach (FilterBase filter in result)
            FilterInputSuggestions.Add(filter);
    }
    
    /// <summary>
    /// TokenItemAdding响应
    /// 注意不可以用async会有bug
    /// </summary>
    /// <param name="args"></param>
    [RelayCommand]
    private void FilterInputTokenItemAdding(TokenItemAddingEventArgs args)
    {
        if (args.Item is FilterBase) return;
        if (string.IsNullOrEmpty(args.TokenText) == false)
        {
            if (FilterInputSuggestions.Count > 1 && !Filters.Contains(FilterInputSuggestions[0]))
            {
                args.Item = FilterInputSuggestions[0];
                return;
            }
        }

        args.Cancel = true;
        _infoService.Info(InfoBarSeverity.Error, msg: "HomePage_Filter_Not_Found".GetLocalized());
    }

    [RelayCommand]
    private void OnFilterFlyoutOpening(object arg)
    {
        UpdateFilterPanelDisplay(null, null!);
    }
    
    private bool ContainNonVirtualGameFilter()
    {
        return Filters.Count > 0 && Filters.Any(f => f.GetType() != typeof(VirtualGameFilter));
    }
    
    partial void OnKeepFiltersChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.KeepFilters, value);

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
        Source.RefreshFilter();
    }

    #endregion

    #region SORT
    private SortKeys[] SortKeysList
    {
        get;
        set;
    } = { SortKeys.LastPlay , SortKeys.Developer};

    private bool[] SortKeysAscending
    {
        get;
        set;
    } = {false, false};

    /// <summary>
    /// 更新sort参数
    /// </summary>
    /// <param name="sortKeysList"></param>
    /// <param name="sortKeysAscending">升序/降序: true/false</param>
    private void UpdateSortKeys(SortKeys[] sortKeysList, bool[] sortKeysAscending)
    {
        SortKeysList = sortKeysList;
        SortKeysAscending = sortKeysAscending;
        if (SortKeysList.Length != SortKeysAscending.Length)
            throw new PvnException("SortKeysList.Length != SortKeysAscending.Length");
        Source.SortDescriptions.Clear();
        for (var i = 0; i < SortKeysList.Length; i++)
        {
            switch (SortKeysList[i])
            {
                case SortKeys.Developer:
                    Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Developer), 
                        SortKeysAscending[i]?SortDirection.Ascending:SortDirection.Descending, 
                        StringComparer.Ordinal
                    ));
                    break;
                case SortKeys.Name:
                    Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Developer), 
                        SortKeysAscending[i]?SortDirection.Descending:SortDirection.Ascending, 
                        StringComparer.CurrentCultureIgnoreCase
                    ));
                    // take *= -1;
                    break;
                case SortKeys.Rating:
                    Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Rating), 
                        SortKeysAscending[i]?SortDirection.Ascending:SortDirection.Descending
                    ));
                    break;
                case SortKeys.LastPlay:
                    Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastPlay), 
                        SortKeysAscending[i]?SortDirection.Ascending:SortDirection.Descending
                    ));
                    break;
                case SortKeys.ReleaseDate:
                    Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.ReleaseDate), 
                        SortKeysAscending[i]?SortDirection.Ascending:SortDirection.Descending
                    ));
                    break;
            }
            
        }
        Source.RefreshSorting();
    }
    
    /// <summary>
    /// 获取并设置galgame排序的关键字
    /// </summary>
    [RelayCommand]
    private async Task Sort()
    
    {
        // Move to homepage
        List<SortKeys> sortKeysList = new()
        {
            SortKeys.Name,
            SortKeys.Developer,
            SortKeys.Rating,
            SortKeys.LastPlay,
            SortKeys.ReleaseDate
        };
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "排序",
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
        };
        
        ComboBox comboBox1 = new()
        {
            Header = "第一关键字",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = sortKeysList,
            Margin = new Thickness(0, 0, 5, 0),
            SelectedItem = SortKeysList[0]
        };
        ToggleSwitch toggleSwitch1 = new()
        {
            Header = "降序/升序",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5, 0, 0, 0),
            OnContent = "升序",
            OffContent = "降序",
            IsOn = SortKeysAscending[0]
        };
        StackPanel panel1 = new ();
        panel1.Children.Add(comboBox1);
        panel1.Children.Add(toggleSwitch1);
        Grid.SetColumn(panel1, 0 );
        
        ComboBox comboBox2 = new()
        {
            Header = "第二关键字",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = sortKeysList,
            Margin = new Thickness(0, 0, 5, 0),
            SelectedItem = SortKeysList[1]
        };
        ToggleSwitch toggleSwitch2 = new()
        {
            Header = "降序/升序",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5, 0, 0, 0),
            OnContent = "升序",
            OffContent = "降序",
            IsOn = SortKeysAscending[1]
        };
        StackPanel panel2 = new ();
        panel2.Children.Add(comboBox2);
        panel2.Children.Add(toggleSwitch2);
        Grid.SetColumn(panel2, 1 );
        

        dialog.PrimaryButtonClick += async (_, _) =>
        {
            UpdateSortKeys(
                new[] { (SortKeys)comboBox1.SelectedItem, (SortKeys)comboBox2.SelectedItem },
                new []{toggleSwitch1.IsOn, toggleSwitch2.IsOn});
            await _localSettingsService.SaveSettingAsync(KeyValues.SortKeys, SortKeysList);
            await _localSettingsService.SaveSettingAsync(KeyValues.SortKeysAscending, SortKeysAscending);
        };
        Grid content = new();
        content.ColumnDefinitions.Add(new ColumnDefinition{Width = new GridLength(1, GridUnitType.Star)});
        content.ColumnDefinitions.Add(new ColumnDefinition{Width = new GridLength(1, GridUnitType.Star)});
        content.Children.Add(panel1);
        content.Children.Add(panel2);
        dialog.Content = content;
        await dialog.ShowAsync();
    }
    

    #endregion

    #region EDIT_MODE

    [NotifyPropertyChangedFor(nameof(GalgameSelectionMode))]
    [ObservableProperty] private bool _isEditMode;

    public ListViewSelectionMode GalgameSelectionMode =>
        _isEditMode ? ListViewSelectionMode.Extended : ListViewSelectionMode.None;

    public readonly ObservableCollection<Galgame> SelectedGalgames = new();

    [RelayCommand]
    private async Task EditBarDeleteGalgame()
    {
        if (SelectedGalgames.Count == 0) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "HomePage_RemoveDialog_Title".GetLocalized(),
            Content = (SelectedGalgames.Count == 1 ? 
                "HomePage_RemoveDialog_GameMessage" : "HomePage_RemoveDialog_GamesMessage").GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            foreach (Galgame g in SelectedGalgames)
            {
                await _galgameService.RemoveGalgame(g);
            }
        };

        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task EditBarGetInfoFromRss()
    {
        if (SelectedGalgames.Count == 0) return;
        IsPhrasing = true;
        foreach (Galgame galgame in SelectedGalgames)
        {
            await _galgameService.PhraseGalInfoAsync(galgame);
        }
        IsPhrasing = false;
    }

    [RelayCommand]
    private void EditBarQuitEditMode()
    {
        IsEditMode = false;
    }

    #endregion

    /// <summary>
    /// 添加Galgame
    /// </summary>
    /// <param name="path">游戏文件夹路径</param>
    private async Task AddGalgameInternal(string path)
    {
        //TODO
        IsPhrasing = true;
        AddGalgameResult result = AddGalgameResult.Other;
        string msg;
        try
        {
            result = await _galgameService.TryAddGalgameAsync(
                new Galgame(GalgameSourceType.LocalFolder, GalgameFolderSource.GetGalgameName(path), path), true);
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
    
    private void OnGalgameLoadedEvent() => Source.Source = _galgameService.Galgames;

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
    private async Task GalFlyOutDelete(Galgame? galgame)
    {
        if(galgame == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "HomePage_RemoveDialog_Title".GetLocalized(),
            Content = "HomePage_RemoveDialog_GameMessage".GetLocalized(),
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
        Source.Refresh();
    }
    
    public async Task<List<FilterBase>> SearchFilters(string str)
    {
        List<FilterBase> result = new();
        if (str.Contains('/'))
            str = str[..(str.LastIndexOf('/') - 1)];
        await Task.Run((async Task() =>
        {
            IList<Galgame> games = (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!.Galgames;
            IEnumerable<CategoryGroup> categoryGroups = await App.GetService<ICategoryService>().GetCategoryGroupsAsync();
            //Category
            HashSet<string> addedCategories = new();
            result.AddRange(from categoryGroup in categoryGroups
                from category in categoryGroup.Categories
                where category.Name.ContainX(str)
                where addedCategories.Add(category.Name)
                select new CategoryFilter(category));
            result.RemoveAll(filter => Filters.Any(f => f is CategoryFilter && f.Name == filter.Name));
            //Tags
            HashSet<string> addedTags = new();
            result.AddRange(from game in games
                from tag in game.Tags.Value ?? new ObservableCollection<string>()
                where tag.ContainX(str)
                where addedTags.Add(tag)
                select new TagFilter(tag));
            result.RemoveAll(filter => Filters.Any(f => f is TagFilter && f.Name == filter.Name));
            //本地游戏
            if(Filters.Any(f => f is VirtualGameFilter) == false)
                result.Add(new VirtualGameFilter());
        })!);
        return result;
    }

    public void Receive(HomePageSetFilterMessage message)
    {
        Filters.Clear();
        Filters.Add(message.Value);
    }
}

public class HomePageSetFilterMessage : ValueChangedMessage<FilterBase>
{
    public HomePageSetFilterMessage(FilterBase filter) : base(filter)
    {        
    }
}


public class GalgameSearchSuggestionsProvider : ISearchSuggestionsProvider
{
    private GalgameCollectionService _galgameCollectionService;
    public GalgameSearchSuggestionsProvider()
    {
        _galgameCollectionService = (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!;
    }
    public async Task<IEnumerable<string>?> GetSearchSuggestionsAsync(string key)
    {
        return await _galgameCollectionService.GetSearchSuggestions(key);
    }
}
