using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class CategoryViewModel : ObservableObject, INavigationAware, ISearchSuggestionsProvider
{
    private readonly CategoryService _categoryService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    // ReSharper disable once CollectionNeverQueried.Global
    // 必须用new ObservableCollection<Category>()初始化
    public readonly AdvancedCollectionView Source = new(new ObservableCollection<Category>());
    
    [ObservableProperty] private ObservableCollection<CategoryGroup> _categoryGroups = new();
    
    private CategoryGroup? _selectedCategoryGroup;
    public CategoryGroup? SelectedCategoryGroup 
    { 
        get => _selectedCategoryGroup;
        set
        {
            if (SetProperty(ref _selectedCategoryGroup, value) && value != null)
            {
                UpdateSourceFromSelectedGroup(value);
            }
        }
    }
    
    private void UpdateSourceFromSelectedGroup(CategoryGroup group)
    {
        ObservableCollection<Category> newCollection = new ObservableCollection<Category>(group.Categories);
        
        Source.Source = newCollection;
        
        CanDeleteCategoryGroup = group.Type == CategoryGroupType.Custom;
        CanAddCategory = group.Type != CategoryGroupType.Status;
        
        CanCombineCategory = group.Type != CategoryGroupType.Status;
        CanDeleteCategory = group.Type != CategoryGroupType.Status;
        
        _ = _localSettingsService.SaveSettingAsync(KeyValues.CategoryGroup, group.Name);
    }
    
    [ObservableProperty] private bool _canDeleteCategoryGroup; //能否删除当前分类组（仅custom分类组能删除）
    [ObservableProperty] private bool _canAddCategory; //能否添加分类（状态分类组不能添加）
    [ObservableProperty] private bool _canCombineCategory; //能否组合分类（仅非custom分类组可以）
    [ObservableProperty] private bool _canDeleteCategory; //能否删除分类（仅非custom分类组可以）

    public CategoryViewModel(ICategoryService categoryService, INavigationService navigationService,
        ILocalSettingsService localSettingsService, IFilterService filterService)
    {
        _categoryService = (categoryService as CategoryService)!;
        _localSettingsService = localSettingsService;
        _navigationService = navigationService;
        _filterService = filterService;
    }

    [RelayCommand]
    private void OnItemClick(Category category)
    {
        NavigationHelper.NavigateToHomePage(_navigationService, _filterService, [new CategoryFilter(category)]);
    }
    
    [RelayCommand]
    private async Task CategoryNow()
    {
        var confirm = false;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "CategoryPage_CategoryNow_ConfirmTitle".GetLocalized(),
            Content = "CategoryPage_CategoryNow_ConfirmMsg".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            PrimaryButtonCommand = new RelayCommand(() => confirm = true),
            DefaultButton = ContentDialogButton.Secondary,
            Width = 400 
        };
        await dialog.ShowAsync();

        if (!confirm) return;
        
        await _categoryService.UpdateAllGames();
        SelectCategoryGroup(await GetCategoryGroup());
    }

    public async void OnNavigatedTo(object parameter)
    {
        Source.Filter = s =>
        {
            if (s is Category source)
            {
                return SearchKey.IsNullOrEmpty() || source.ApplySearchKey(SearchKey);
            }

            return false;
        };
        CategoryGroups = await _categoryService.GetCategoryGroupsAsync();
        
        // 仅在首次进入或显式传参时更新分组，避免返回页面时重建集合导致滚动位置丢失
        if (parameter is CategoryGroup paramGroup)
        {
            if (SelectedCategoryGroup != paramGroup)
                SelectedCategoryGroup = paramGroup;
        }
        else if (SelectedCategoryGroup is null)
        {
            SelectedCategoryGroup = await GetCategoryGroup();
        }

        // 页面缓存：再次进入页面时刷新当前集合与按钮状态，保持滚动位置
        if (SelectedCategoryGroup is not null)
        {
            CategoryGroup? latest = CategoryGroups.FirstOrDefault(c => c.Id == SelectedCategoryGroup.Id) ?? SelectedCategoryGroup;
            if (Source.Source is ObservableCollection<Category> collection)
                collection.SyncCollection(latest.Categories);
            
            CanDeleteCategoryGroup = latest.Type == CategoryGroupType.Custom;
            CanAddCategory = latest.Type != CategoryGroupType.Status;
            CanCombineCategory = latest.Type != CategoryGroupType.Status;
            CanDeleteCategory = latest.Type != CategoryGroupType.Status;
            
            Source.RefreshFilter();
        }
    }

    /// <summary>
    /// 获取设置中要显示的分类组
    /// </summary>
    private async Task<CategoryGroup> GetCategoryGroup()
    {
        var groupStr = await _localSettingsService.ReadSettingAsync<string>(KeyValues.CategoryGroup);
        CategoryGroup? result = CategoryGroups.FirstOrDefault(c => c.Name == groupStr);
        if (result == null)
        {
            result = _categoryService.StatusGroup;
            await _localSettingsService.SaveSettingAsync(KeyValues.CategoryGroup, result.Name);
        }
        return result;
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private async Task DeleteCategory(Category category)
    {
        var delete = false;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "CategoryPage_DeleteCategory_Title".GetLocalized(),
            Content = "CategoryPage_DeleteCategory_Msg".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            PrimaryButtonCommand = new RelayCommand(() => delete = true),
            DefaultButton = ContentDialogButton.Secondary
        };
        await dialog.ShowAsync();
        if (!delete) return;
        
        _categoryService.DeleteCategory(category);
        Source.Remove(category);
    }

    [RelayCommand]
    private async Task CombineCategory(Category source)
    {
        if (SelectedCategoryGroup == null) return;
        CombineCategoryDialog dialog = new(SelectedCategoryGroup, source);
        await dialog.ShowAsync();
        if (dialog.Target == null) return;
        _categoryService.Merge(dialog.Target, source);
        Source.Remove(source);
    }

    private class CombineCategoryDialog : ContentDialog
    {
        public Category? Target;
        public CombineCategoryDialog(CategoryGroup group, Category source)
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot;
            Title = "CategoryPage_CombineCategory_Title".GetLocalized();

            StackPanel panel = new();
            panel.Children.Add(new TextBlock { Text = "CategoryPage_CombineCategory_Msg".GetLocalized() });
            ComboBox comboBox = new();
            List<Category> categories = new();
            group.Categories.ForEach(c => categories.Add(c));
            categories.Remove(source);
            comboBox.ItemsSource = categories;
            comboBox.HorizontalAlignment = HorizontalAlignment.Center;
            comboBox.Margin = new Thickness(0, 10, 0, 0);
            panel.Children.Add(comboBox);
            Content = panel;
            
            PrimaryButtonText = "Yes".GetLocalized();
            SecondaryButtonText = "Cancel".GetLocalized();
            PrimaryButtonCommand = new RelayCommand(() => Target = comboBox.SelectedItem as Category);
            SecondaryButtonCommand = new RelayCommand(() => Target = null);
            DefaultButton = ContentDialogButton.Secondary;
        }
    }

    [RelayCommand]
    private void EditCategory(Category category)
    {
        _navigationService.NavigateTo(typeof(CategorySettingViewModel).FullName!, category);
        // _categoryService.UpdateCategory(category);
    }

    /// <summary>
    /// 选择当前要展示的分类组（更新显示），并将当前选中的组保存到设置中
    /// </summary>
    /// <param name="group">分类组</param>
    [RelayCommand]
    private void SelectCategoryGroup(CategoryGroup group)
    {
        UpdateSourceFromSelectedGroup(group);
    }

    [RelayCommand]
    private async Task AddCategory()
    {
        var name = string.Empty;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "CategoryPage_AddCategoryDialog_Title".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Content = new TextBox
            {
                Header = "CategoryPage_AddCategoryDialog_Msg".GetLocalized(),
                Text = name
            }
        };
        dialog.PrimaryButtonClick += (_, _) =>
        {
            if (SelectedCategoryGroup is null) return;
            name = (dialog.Content as TextBox)!.Text;
            Category category = new(name);
            SelectedCategoryGroup.Categories.Add(category);
            Source.Add(category);
        };
        
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task AddCategoryGroup()
    {
        var name = string.Empty;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "CategoryPage_AddCategoryGroupDialog_Title".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Content = new TextBox
            {
                Header = "CategoryPage_AddCategoryGroupDialog_Msg".GetLocalized(),
                Text = name
            }
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            name = (dialog.Content as TextBox)!.Text;
            CategoryGroup group = _categoryService.AddCategoryGroup(name);
            CategoryGroups = await _categoryService.GetCategoryGroupsAsync();
            SelectCategoryGroup(group);
        };
        
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task DeleteCategoryGroup()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "CategoryPage_DeleteCategoryGroupDialog_Title".GetLocalized(),
            Content = "CategoryPage_DeleteCategoryGroupDialog_Msg".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Secondary
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            if (SelectedCategoryGroup == null) return;
            _categoryService.DeleteCategoryGroup(SelectedCategoryGroup);
            CategoryGroups = await _categoryService.GetCategoryGroupsAsync();
            SelectCategoryGroup(_categoryService.StatusGroup);
        };
        
        await dialog.ShowAsync();
    }
    
    #region SERACH
    
    [ObservableProperty] private string _searchTitle = "Search".GetLocalized();
    [ObservableProperty] private string _searchKey = "";
    [ObservableProperty] private ObservableCollection<string> _searchSuggestions = [];
    
    [RelayCommand]
    private void Search(string searchKey)
    {
        SearchTitle = searchKey == string.Empty ? "Search".GetLocalized() : "Search".GetLocalized() + " ●";
        Source.RefreshFilter();
    }
    
    #endregion

    public async Task<IEnumerable<string>?> GetSearchSuggestionsAsync(string key)
    {
        await Task.CompletedTask;
        return from category in  SelectedCategoryGroup?.Categories
            where category.Name.ContainX(key)
            select category.Name;
    }
}