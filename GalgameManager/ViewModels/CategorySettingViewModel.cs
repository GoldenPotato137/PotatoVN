using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.ViewModels;

public partial class CategorySettingViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly CategoryService _categoryService;
    private readonly GalgameCollectionService _galgameCollectionService;
    public Category Category = new();
    public ObservableCollection<CategoryGroupChecker> CategoryGroups = new();
    public ObservableCollection<GameChecker> Games = new();

    public CategorySettingViewModel(INavigationService navigationService, ICategoryService categoryService, 
        IDataCollectionService<Galgame> dataCollectionService)
    {
        _navigationService = navigationService;
        _categoryService = (CategoryService)categoryService;
        _galgameCollectionService = (GalgameCollectionService)dataCollectionService;
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is Category category)
        {
            Category = category;
            ObservableCollection<CategoryGroup> tmpCategoryGroups = await _categoryService.GetCategoryGroupsAsync();
            foreach (CategoryGroup group in tmpCategoryGroups)
            {
                CategoryGroups.Add(new CategoryGroupChecker
                {
                    Group = group,
                    IsSelect = group.Categories.Contains(Category)
                });
            }
            List<Galgame> games = _galgameCollectionService.Galgames;
            foreach (Galgame game in games)
            {
                Games.Add(new GameChecker()
                {
                    Game = game,
                    IsSelect = game.Categories.Contains(Category)
                });
            }
        }
        else
            throw new ArgumentException("parameter is not Category");
    }

    public void OnNavigatedFrom()
    {
        foreach (CategoryGroupChecker groupChecker in CategoryGroups)
        {
            if (groupChecker.IsSelect && groupChecker.Group.Categories.Contains(Category) == false)
                groupChecker.Group.Categories.Add(Category);
            else if (groupChecker.IsSelect == false && groupChecker.Group.Categories.Contains(Category))
                groupChecker.Group.Categories.Remove(Category);
        }

        foreach (GameChecker gameChecker in Games)
        {
            if (gameChecker.IsSelect && Category.Belong(gameChecker.Game) == false)
                Category.Add(gameChecker.Game);
            else if (gameChecker.IsSelect == false && Category.Belong(gameChecker.Game))
                Category.Remove(gameChecker.Game);
        }
    }

    [RelayCommand]
    private void Back()
    {
        _navigationService.GoBack();
    }

    [RelayCommand]
    private async Task PickImage()
    {
        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".bmp");
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file is not null)
            Category.ImagePath = file.Path;
    }
}

public class CategoryGroupChecker
{
    public CategoryGroup Group = new();
    public bool IsSelect;
}

public class GameChecker
{
    public Galgame Game = new();
    public bool IsSelect;
}