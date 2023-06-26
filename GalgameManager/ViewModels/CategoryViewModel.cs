using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.Services;

namespace GalgameManager.ViewModels;

public partial class CategoryViewModel : ObservableRecipient, INavigationAware
{
    private readonly CategoryService _categoryService;
    private readonly INavigationService _navigationService;
    public ObservableCollection<Category> Source = new();
    private ObservableCollection<CategoryGroup> _categoryGroups = new();

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
}