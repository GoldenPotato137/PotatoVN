using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;

namespace GalgameManager.ViewModels;

public partial class UpdateContentViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private string _updateContent = string.Empty;
    private readonly IUpdateService _updateService;
    [ObservableProperty] private Visibility _isDownloading = Visibility.Collapsed;
    [ObservableProperty] private Visibility _displayTitle = Visibility.Collapsed;
    [ObservableProperty] private string _errorMsg = string.Empty;
    [ObservableProperty] private bool _errorVisibility;
    [ObservableProperty] private string _currentVersion =
        "UpdateContentPage_CurrentVersion".GetLocalized() + RuntimeHelper.GetVersion();

    public UpdateContentViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
        _updateService.DownloadEvent += () => IsDownloading = Visibility.Visible;
        _updateService.DownloadCompletedEvent += () => IsDownloading = Visibility.Collapsed;
        _updateService.DownloadFailedEvent += async s =>
        {
            ErrorVisibility = true;
            IsDownloading = Visibility.Collapsed;
            ErrorMsg = s;
            await Task.Delay(2000);
            ErrorVisibility = false;
        };
    }

    public async void OnNavigatedTo(object parameter)
    {
        UpdateContent = await _updateService.GetUpdateContentAsync();
        if (parameter is bool displayTitle)
            DisplayTitle = displayTitle ? Visibility.Visible : Visibility.Collapsed;
    }

    public void OnNavigatedFrom()
    {
    }
    
    [RelayCommand]
    private async Task OnDownloadClick()
    {
        UpdateContent = await _updateService.GetUpdateContentAsync();
    }

    [RelayCommand]
    private async Task OnLinkClick(LinkClickedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(e.Link));
    }

}