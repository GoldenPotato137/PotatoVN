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
    public ObservableCollection<Category> Source = new();
    private ObservableCollection<CategoryGroup> _categoryGroups = new();
    private CategoryGroup? _currentGroup;

    public CategoryViewModel(ICategoryService categoryService, INavigationService navigationService)
    {
        _categoryService = (categoryService as CategoryService)!;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private void OnItemClick(Category category)
    {
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!, new CategoryFilter(category));
    }

    private void UpdateSource()
    {
        Source.Clear();
        // 暂时展示开发商分类
        CategoryGroup developer = _categoryGroups.First(c => c.Type == CategoryGroupType.Developer);
        _currentGroup = developer;
        developer.Categories.ForEach(c => Source.Add(c));
    }

    public async void OnNavigatedTo(object parameter)
    {
        _categoryGroups = await _categoryService.GetCategoryGroupsAsync();
        UpdateSource();
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private async void DeleteCategory(Category category)
    {
        var delete = false;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
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
        UpdateSource();
    }

    [RelayCommand]
    private async void CombineCategory(Category source)
    {
        if (_currentGroup == null) return;
        CombineCategoryDialog dialog = new(_currentGroup, source);
        await dialog.ShowAsync();
        if (dialog.Target == null) return;
        _categoryService.Merge(dialog.Target, source);
        UpdateSource();
    }

    private class CombineCategoryDialog : ContentDialog
    {
        public Category? Target;
        public CombineCategoryDialog(CategoryGroup group, Category source)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot;
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
    private void UpdateCategory(Category category)
    {
        _categoryService.UpdateCategory(category);
    }
}