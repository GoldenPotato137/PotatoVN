using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.MultiStreamPage.Lists;
using GalgameManager.ViewModels;
using LiteDB;
using Newtonsoft.Json;
using SourceFilter = GalgameManager.Models.Filters.SourceFilter;
using GalgameManager.Helpers.Converter;

namespace GalgameManager.ViewModels
{
    public partial class MultiStreamViewModel : ObservableRecipient, INavigationAware
    {
        public static bool IsSetting { get; private set; }
        public ObservableCollection<IList> Lists { get; } = new();
        [ObservableProperty][NotifyPropertyChangedFor(nameof(ScrollMode))] 
        private bool _allowScroll;
        private bool _isRetry;
        private ILiteCollection<ListDbDto> _dbSet = null!;
        public ScrollMode ScrollMode => AllowScroll ? ScrollMode.Enabled : ScrollMode.Disabled;

        private readonly IGalgameCollectionService _gameService;
        private static IGalgameSourceCollectionService _sourceService = null!;
        private static ICategoryService _categoryService = null!;
        private readonly INavigationService _navigationService;
        private readonly IFilterService _filterService;
        private readonly IInfoService _infoService;
        private readonly ILocalSettingsService _settingsService;
        private readonly List<JsonConverter> _converters = new();

        public MultiStreamViewModel(IGalgameCollectionService gameService, ICategoryService categoryService,
            INavigationService navigationService, IFilterService filterService, IInfoService infoService,
            IGalgameSourceCollectionService sourceService, ILocalSettingsService localSettingsService)
        {
            _gameService = gameService;
            _categoryService = categoryService;
            _navigationService = navigationService;
            _filterService = filterService;
            _sourceService = sourceService;
            _infoService = infoService;
            _settingsService = localSettingsService;
            
            _converters.Add(new CategoryAndUuidConverter(categoryService));
            _converters.Add(new CategoryGroupAndUuidConverter(categoryService));
            _converters.Add(new GalgameSourceAndUrlConverter(sourceService));
        }

        [RelayCommand]
        private async Task Load()
        {
            try
            {
                _dbSet = _settingsService.Database.GetCollection<ListDbDto>("homepage_list");
                await LiteDbUpgrade();
                List<ListDbDto> tmp = _dbSet.FindAll().OrderBy(l => l.Order).ToList();
                foreach (ListDbDto list in tmp)
                {
                    IList? target = null;
                    if (list is GameListDbDto gameList)
                    {
                        Category? category = LoadObj(list, gameList.CategoryId,
                            () => _categoryService.GetCategory(gameList.CategoryId!.Value));
                        GalgameSourceBase? source = LoadObj(list, gameList.SourceId,
                            () => _sourceService.GetGalgameSources().FirstOrDefault(s => s.Id == gameList.SourceId));
                        target = new GameList(list.Title, list.Sort, category, source);
                    }
                    else if (list is CategoryListDbDto categoryList)
                    {
                        CategoryGroup? group = LoadObj(list, categoryList.GroupId, () => _categoryService
                            .GetCategoryGroupsAsync().Result.FirstOrDefault(g => g.Id == categoryList.GroupId));
                        if (group is null) continue;
                        target = new CategoryList(group);
                    }
                    else if (list is SourceListDbDto sourceList)
                    {
                        GalgameSourceBase? source = LoadObj(list, sourceList.RootId,
                            () => _sourceService.GetGalgameSources().FirstOrDefault(s => s.Id == sourceList.RootId));
                        target = new SourceList(source);
                    }
                    Debug.Assert(target is not null);
                    target.Id = list.Id;
                    target.Title = list.Title;
                    target.Sort = list.Sort;
                    target.Refresh();
                    Lists.Add(target);
                }
                AllowScroll = await _settingsService.ReadSettingAsync<bool>(KeyValues.MultiStreamPageAllowScroll);
                Lists.CollectionChanged += ListsOnCollectionChanged;
                GameToOpacityConverter.SpecialDisplayVirtualGame = await _settingsService.ReadSettingAsync<bool>(KeyValues.SpecialDisplayVirtualGame);
            }
            catch (COMException e) // 奇怪bug，暂时无法解决，重新加载界面
            {
                if (_isRetry)
                    _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Page reload retry failed", e);
                else
                    _infoService.DeveloperEvent(InfoBarSeverity.Warning, "Page reload failed. Trying reloading", e);
                _navigationService.NavigateTo(typeof(InfoViewModel).FullName!);
                _navigationService.NavigateTo(typeof(MultiStreamViewModel).FullName!, true);
            }
            catch (Exception e) // 不应该发生
            {
                _infoService.Event(EventType.PageError, InfoBarSeverity.Error, title: "Something went wrong", e);
            }
            return;
            
            T? LoadObj<T> (ListDbDto dto, Guid? id, Func<T?> func) where T : class
            {
                if (id is null) return null;
                T? result = func();
                if (result is null)
                    _infoService.Event(EventType.PageError, InfoBarSeverity.Warning,
                        title: "MultiStreamPage_LoadFailed".GetLocalized(),
                        msg: "MultiStreamPage_LoadFailed_CanNotFindById".GetLocalized(dto.Title));
                return result;
            }
        }
        
        public void OnNavigatedTo(object parameter)
        {
            if (parameter is bool b) _isRetry = b;
            // 由于页面开启了缓存，再次进入时需要：
            // 1) 重新订阅集合变更事件（OnNavigatedFrom 中已取消订阅）
            // 2) 刷新现有列表的数据源与排序，确保内容最新
            Lists.CollectionChanged -= ListsOnCollectionChanged;
            Lists.CollectionChanged += ListsOnCollectionChanged;
            foreach (IList list in Lists)
                list.Refresh();
        }

        public void OnNavigatedFrom()
        {
            Lists.CollectionChanged -= ListsOnCollectionChanged;
        }

        public static List<IList> GetInitList()
        {
            List<IList> result = new();
            result.Add(new GameList("MultiStreamPage_RecentPlayGames".GetLocalized(),
                MultiStreamPageSortKeys.LastPlayed));
            result.Add(new CategoryList(_categoryService.DeveloperGroup));
            CategoryGroup statusGroup = _categoryService.StatusGroup;
            result.Add(new GameList(statusGroup.Categories[2].Name, MultiStreamPageSortKeys.LastPlayed,
                statusGroup.Categories[2])); // 在玩
            result.Add(new GameList(statusGroup.Categories[1].Name, MultiStreamPageSortKeys.LastPlayed, 
                statusGroup.Categories[1])); // 已完成
            result.Add(new GameList(statusGroup.Categories[0].Name, MultiStreamPageSortKeys.LastPlayed,
                statusGroup.Categories[0])); // 未设置
            result.Add(new SourceList(null));
            return result;
        }

        [RelayCommand]
        private void ClickGame(Galgame? clickedItem)
        {
            if (clickedItem == null) return;
            NavigationHelper.NavigateToGalgamePage(_navigationService,
                new GalgamePageParameter { Galgame = clickedItem });
        }

        [RelayCommand]
        private void ClickCategory(Category category)
        {
            NavigationHelper.NavigateToHomePage(_navigationService, _filterService,
                new[] { new CategoryFilter(category) });
            _categoryService.Save(category);
        }

        [RelayCommand]
        private void ClickSource(GalgameSourceBase source)
        {
            NavigationHelper.NavigateToHomePage(_navigationService, _filterService, new[] { new SourceFilter(source) });
            _sourceService.Save(source);
        }

        [RelayCommand]
        private async Task Setting()
        {
            Views.Dialog.MultiStreamPageSettingDialog dialog = new(Lists);
            IsSetting = true;
            ContentDialogResult status = await dialog.ShowAsync();
            IsSetting = false;
            if (status != ContentDialogResult.Primary) return;
            Lists.SyncCollection(dialog.Result, true);
            foreach (IList list in Lists)
                list.Refresh();
            SaveList(Lists.ToList());
        }
        
        private void SaveList(List<IList> lists)
        {
            for (var i = 0; i < lists.Count; i++)
            {
                IList list = lists[i];
                list.Order = i;
                _dbSet.Upsert(list.ToDbDto());
            }
            List<ListDbDto> tmp = _dbSet.FindAll().ToList();
            foreach (ListDbDto dto in tmp.Where(dto => lists.All(l => l.Id != dto.Id)))
                _dbSet.Delete(dto.Id);
        }

        partial void OnAllowScrollChanged(bool value)
        {
            _settingsService.SaveSettingAsync(KeyValues.MultiStreamPageAllowScroll, value);
        }
        
        private void ListsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            SaveList(Lists.ToList());

        #region SEARCH

        [ObservableProperty]
        private GalgameSearchSuggestionsProvider _galgameSearchSuggestionsProvider = new(true, true, false, true, true);
        [ObservableProperty] private string _searchKey = string.Empty;

        [RelayCommand]
        private void Search()
        {
            Galgame? game = _gameService.GetGalgameFromName(SearchKey);
            if (game is not null)
            {
                NavigationHelper.NavigateToGalgamePage(_navigationService, new GalgamePageParameter { Galgame = game });
                return;
            }
            Category? category = _categoryService.GetCategory(SearchKey);
            if (category is not null)
            {
                NavigationHelper.NavigateToHomePage(_navigationService, _filterService, new[] { new CategoryFilter(category) });
                return;
            }
            _infoService.Info(InfoBarSeverity.Error, msg:"MultiStreamPage_SearchFailed".GetLocalized());
        }

        #endregion

        #region UPGRADE

        private async Task LiteDbUpgrade()
        {
            LocalSettingStatus status =
                await _settingsService.ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true) ?? new();
            if (status.MultiStreamPageLiteDbUpgrade) return;
            try
            {
                List<IList> tmp = await _settingsService.ReadSettingAsync<List<IList>>(
                    KeyValues.MultiStreamPageList,
                    true, _converters, true) ?? GetInitList();
                tmp = tmp.Count == 0 ? GetInitList() : tmp; // 崩溃时会保存空表，重新初始化
                SaveList(tmp);
                await _settingsService.RemoveSettingAsync(KeyValues.MultiStreamPageList, true);
            }
            catch (Exception e)
            {
                _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "LiteDb upgrade failed", e);
            }
            status.MultiStreamPageLiteDbUpgrade = true;
            await _settingsService.SaveSettingAsync(KeyValues.DataStatus, status, true);
        }

        #endregion
    }

    public class MultiStreamPageSelector : DataTemplateSelector
    {
        public DataTemplate GameListTemplate { get; set; } = null!;
        public DataTemplate CategoryListTemplate { get; set; } = null!;
        public DataTemplate SourceListTemplate { get; set; } = null!;

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            Debug.Assert(
                GameListTemplate is not null && CategoryListTemplate is not null && SourceListTemplate is not null,
                "Template is not set");
            return item switch
            {
                GameList => GameListTemplate,
                CategoryList => CategoryListTemplate,
                SourceList => SourceListTemplate,
                _ => base.SelectTemplateCore(item, container)
            };
        }
    }
}


# region OBSERVABLE_LISTS

namespace GalgameManager.MultiStreamPage.Lists
{
    public enum MultiStreamPageSortKeys
    {
        LastPlayed,
        ReleaseDate,
        LastClicked,
    }

    public interface IList
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public MultiStreamPageSortKeys Sort { get; set; }
        public int Order { get; set; }
        public void Refresh();
        public ListDbDto ToDbDto();
    }
    
    public partial class GameList : ObservableRecipient, IList
    {
        [JsonIgnore] public IRelayCommand ClickTitleCommand; //为什么不直接用[RelayCommand]:因为没法JsonIgnore
        [JsonIgnore] public AdvancedCollectionView Games;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private MultiStreamPageSortKeys _sort;
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Order { get; set; }

        [ObservableProperty] private Category? _category; // 如果设置了则为某分类下的游戏列表 
        [ObservableProperty] private GalgameSourceBase? _source; // 如果设置了则为某源下的游戏列表

        public GameList()
        {
            Games = new AdvancedCollectionView(new ObservableCollection<Galgame>(), true);
            ClickTitleCommand = new RelayCommand(ClickTitle);
        }

        public GameList(string title, MultiStreamPageSortKeys sort, Category? category = null,
            GalgameSourceBase? source = null) : this()
        {
            
            if (category is null && source is null)
                Games.Source = App.GetService<IGalgameCollectionService>().Galgames;
            Title = title;
            Sort = sort;
            Category = category;
            Source = source;
            Refresh();
        }

        private void ClickTitle()
        {
            INavigationService service = App.GetService<INavigationService>();
            if (Category is not null)
                NavigationHelper.NavigateToHomePage(service, App.GetService<IFilterService>(),
                    new[] { new CategoryFilter(Category) });
            else if (Source is not null)
                NavigationHelper.NavigateToHomePage(service, App.GetService<IFilterService>(),
                    new[] { new SourceFilter(Source) });
            else
                NavigationHelper.NavigateToHomePage(service);
        }
        
        public void Refresh()
        {
            // 更新游戏列表
            if (Category is not null)
                (Games.Source as ObservableCollection<Galgame>)?.SyncCollection(Category.GalgamesX);
            else if (Source is not null)
                (Games.Source as ObservableCollection<Galgame>)?.SyncCollection(
                    new List<Galgame>(Source.Galgames.Select(g => g.Galgame)));
            else // 全部游戏
            {
                if (Games.Source.Count == 0) //初始化进来的
                    Games.Source = App.GetService<IGalgameCollectionService>().Galgames;
                // else 不需要更新（因为直接用的galgameCollectionService的可观测游戏列表）
            }

            Games.SortDescriptions.Clear();
            Games.SortDescriptions.Add(new SortDescription(Sort switch
            {
                MultiStreamPageSortKeys.LastPlayed => nameof(Galgame.LastPlayTime),
                MultiStreamPageSortKeys.ReleaseDate => nameof(Galgame.ReleaseDate),
                _ => throw new ArgumentOutOfRangeException()
            }, SortDirection.Descending));
        }

        public ListDbDto ToDbDto() => new GameListDbDto
        {
            Id = Id,
            Title = Title,
            Sort = Sort,
            Order = Order,
            CategoryId = Category?.Id,
            SourceId = Source?.Id,
        };

        partial void OnCategoryChanged(Category? value)
        {
            if (!MultiStreamViewModel.IsSetting) return;
            Title = value?.Name ?? Title;
        }
        
        partial void OnSourceChanged(GalgameSourceBase? value)
        {
            if (!MultiStreamViewModel.IsSetting) return;
            Title = value?.Name ?? Title;
        }
    }

    public partial class CategoryList : ObservableRecipient, IList
    {
        [JsonIgnore] public IRelayCommand ClickTitleCommand; //为什么不直接用[RelayCommand]:因为没法JsonIgnore
        [JsonIgnore] public AdvancedCollectionView Categories;
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Order { get; set; }
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private MultiStreamPageSortKeys _sort;
        [ObservableProperty] private CategoryGroup _group = null!;

        public CategoryList()
        {
            ClickTitleCommand = new RelayCommand(ClickTitle);
            Categories = new AdvancedCollectionView(new ObservableCollection<Category>(), true);
        }

        public CategoryList(CategoryGroup group) : this()
        {
            _group = group;
            Title = group.Name;
            Refresh();
        }

        private void ClickTitle()
        {
            INavigationService service = App.GetService<INavigationService>();
            service.NavigateTo(typeof(CategoryViewModel).FullName!, Group);
        }
        
        public void Refresh()
        {
            (Categories.Source as ObservableCollection<Category>)?.SyncCollection(Group.Categories);
            // 更新排序关键字

            Categories.SortDescriptions.Clear();
            Categories.SortDescriptions.Add(new SortDescription(Sort switch
            {
                MultiStreamPageSortKeys.LastPlayed => nameof(Category.LastPlayed),
                MultiStreamPageSortKeys.LastClicked => nameof(Category.LastClicked),
                _ => throw new ArgumentOutOfRangeException(),
            }, SortDirection.Descending));
        }

        public ListDbDto ToDbDto() => new CategoryListDbDto
        {
            Id = Id,
            Title = Title,
            Sort = Sort,
            Order = Order,
            GroupId = Group.Id,
        };

        partial void OnGroupChanged(CategoryGroup value)
        {
            if (!MultiStreamViewModel.IsSetting) return;
            Title = value.Name;
        }
    }

    public partial class SourceList : ObservableRecipient, IList
    {
        [JsonIgnore] public IRelayCommand ClickTitleCommand; //为什么不直接用[RelayCommand]:因为没法JsonIgnore
        [JsonIgnore] public AdvancedCollectionView Sources;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private MultiStreamPageSortKeys _sort;
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Order { get; set; }
        [ObservableProperty] private GalgameSourceBase? _root;

        private readonly IGalgameSourceCollectionService _sourceService =
            App.GetService<IGalgameSourceCollectionService>();
        
        public SourceList()
        {
            ClickTitleCommand = new RelayCommand(ClickTitle);
            Sources = new AdvancedCollectionView(new ObservableCollection<GalgameSourceBase>(), true);
        }

        /// <param name="root">显示root内的游戏库，若设置为null则显示整个所有库</param>
        public SourceList(GalgameSourceBase? root) : this()
        {
            Root = root;
            Title = Root?.Name ?? "MultiStreamPage_AllSources".GetLocalized();
            Refresh();
        }

        private void ClickTitle()
        {
            INavigationService service = App.GetService<INavigationService>();
            service.NavigateTo(typeof(LibraryViewModel).FullName!, Root);
        }

        public void Refresh()
        {
            Sources.Clear();
            if (Root is null)
            {
                foreach (GalgameSourceBase source in _sourceService.GetGalgameSources())
                    Sources.Add(source);
            }
            else
            {
                foreach (GalgameSourceBase source in Root.SubSources)
                    Sources.Add(source);
            }
            // 更新排序关键字
            Sources.SortDescriptions.Clear();
            Sources.SortDescriptions.Add(new SortDescription(Sort switch
            {
                MultiStreamPageSortKeys.LastPlayed => nameof(GalgameSourceBase.LastPlayed),
                MultiStreamPageSortKeys.LastClicked => nameof(GalgameSourceBase.LastClicked),
                _ => throw new ArgumentOutOfRangeException(),
            }, SortDirection.Descending));
        }

        public ListDbDto ToDbDto() => new SourceListDbDto
        {
            Id = Id,
            Title = Title,
            Sort = Sort,
            Order = Order,
            RootId = Root?.Id,
        };

        partial void OnRootChanged(GalgameSourceBase? value)
        {
            if (!MultiStreamViewModel.IsSetting) return;
            Title = value?.Name ?? "MultiStreamPage_AllSources".GetLocalized();
        }
    }

    public abstract class ListDbDto
    {
        [BsonId] public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public MultiStreamPageSortKeys Sort { get; set; }
        public int Order { get; set; }
    }

    public class GameListDbDto : ListDbDto
    {
        public Guid? CategoryId { get; set; }
        public Guid? SourceId { get; set; }
    }
    
    public class CategoryListDbDto : ListDbDto
    {
        public Guid GroupId { get; set; }
    }

    public class SourceListDbDto : ListDbDto
    {
        public Guid? RootId { get; set; }
    }
}

# endregion