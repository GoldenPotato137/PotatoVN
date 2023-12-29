using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

public partial class CategoryViewModel : ObservableRecipient, INavigationAware
{
    private readonly CategoryService _categoryService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly ObservableCollection<Category> Source = new();
    private ObservableCollection<CategoryGroup> _categoryGroups = new();
    private CategoryGroup? _currentGroup;
    [ObservableProperty] private bool _canDeleteCategoryGroup; //能否删除当前分类组（仅custom分类组能删除）
    [ObservableProperty] private bool _canAddCategory; //能否添加分类（状态分类组不能添加）

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
        _filterService.ClearFilters();
        _filterService.AddFilter(new CategoryFilter(category));
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }

    public async void OnNavigatedTo(object parameter)
    {
        _categoryGroups = await _categoryService.GetCategoryGroupsAsync();
        await SelectCategoryGroup(await GetCategoryGroup());
    }

    // 并不符合MVVM要求，但暂时没有更好的办法
    public void UpdateCategoryGroupFlyout(MenuFlyout? categoryGroupFlyout)
    {
        if (categoryGroupFlyout == null) return;
        categoryGroupFlyout.Items.Clear();
        foreach (CategoryGroup group in _categoryGroups)
            categoryGroupFlyout.Items.Add(new MenuFlyoutItem
            {
                Text = group.Name,
                Command = SelectCategoryGroupCommand,
                CommandParameter = group
            });
    }

    /// <summary>
    /// 获取设置中要显示的分类组
    /// </summary>
    private async Task<CategoryGroup> GetCategoryGroup()
    {
        var groupStr = await _localSettingsService.ReadSettingAsync<string>(KeyValues.CategoryGroup);
        CategoryGroup? result = _categoryGroups.FirstOrDefault(c => c.Name == groupStr);
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
        if (_currentGroup == null) return;
        CombineCategoryDialog dialog = new(_currentGroup, source);
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
    private async Task SelectCategoryGroup(CategoryGroup group)
    {
        _currentGroup = group;
        Source.Clear();
        _currentGroup!.Categories.ForEach(c => Source.Add(c));
        CanDeleteCategoryGroup = _currentGroup.Type == CategoryGroupType.Custom;
        CanAddCategory = _currentGroup.Type != CategoryGroupType.Status;
        await _localSettingsService.SaveSettingAsync(KeyValues.CategoryGroup, group.Name);
    }

    [RelayCommand]
    private async Task AddCategory()
    {
        var name = string.Empty;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
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
            if (_currentGroup is null) return;
            name = (dialog.Content as TextBox)!.Text;
            Category category = new(name);
            _currentGroup.Categories.Add(category);
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
            _categoryGroups = await _categoryService.GetCategoryGroupsAsync();
            await SelectCategoryGroup(group);
        };
        
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task DeleteCategoryGroup()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "CategoryPage_DeleteCategoryGroupDialog_Title".GetLocalized(),
            Content = "CategoryPage_DeleteCategoryGroupDialog_Msg".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Secondary
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            if (_currentGroup == null) return;
            _categoryService.DeleteCategoryGroup(_currentGroup);
            _categoryGroups = await _categoryService.GetCategoryGroupsAsync();
            await SelectCategoryGroup(_categoryService.StatusGroup);
        };
        
        await dialog.ShowAsync();
    }
}