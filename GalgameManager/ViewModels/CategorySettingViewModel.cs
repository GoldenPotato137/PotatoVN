using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;

namespace GalgameManager.ViewModels;

public partial class CategorySettingViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    public Category Category = new();
    
    public CategorySettingViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }
    
    public void OnNavigatedTo(object parameter)
    {
        if (parameter is Category category)
            Category = category;
        else
            throw new ArgumentException("parameter is not Category");
    }

    public void OnNavigatedFrom()
    {
        
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