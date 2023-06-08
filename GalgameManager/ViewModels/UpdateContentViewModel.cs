using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class UpdateContentViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private string _updateContent = string.Empty;
    private readonly IUpdateService _updateService;
    private readonly ILocalSettingsService _localSettingsService;
    [ObservableProperty] private Visibility _isDownloading = Visibility.Collapsed;
    [ObservableProperty] private Visibility _displayTitle = Visibility.Collapsed;
    [ObservableProperty] private string _currentVersion =
        "UpdateContentPage_CurrentVersion".GetLocalized() + RuntimeHelper.GetVersion();

    [ObservableProperty] private string _infoBarMsg = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _infoBarOpen; 

    public UpdateContentViewModel(IUpdateService updateService,ILocalSettingsService localSettingsService)
    {
        _updateService = updateService;
        _localSettingsService = localSettingsService;
        localSettingsService.OnSettingChanged += OnSettingChanged;
        _updateService.DownloadEvent += () => IsDownloading = Visibility.Visible;
        _updateService.DownloadCompletedEvent += () => IsDownloading = Visibility.Collapsed;
        _updateService.DownloadFailedEvent += async s =>
        {
            await ShowInfoBarAsync(s, InfoBarSeverity.Error);
        };
    }

    private async void OnSettingChanged(string key, object value)
    {
        switch (key)
        {
            case KeyValues.UploadData:
                if (value is true)
                    await ShowInfoBarAsync("UpdateContentPage_AppCenterSuccess".GetLocalized(), InfoBarSeverity.Success);
                break;
        }
    }

    private async Task ShowInfoBarAsync(string msg, InfoBarSeverity severity)
    {
        InfoBarMsg = msg;
        InfoBarSeverity = severity;
        InfoBarOpen = true;
        await Task.Delay(4000);
        InfoBarOpen = false;
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

    private async Task DealWithCommand(string command)
    {
        switch (command)
        {
            case "PotatoVN.TurnOnAppCenter":
                await _localSettingsService.SaveSettingAsync(KeyValues.UploadData, true);
                break;
        }
    }
    
    [RelayCommand]
    private async Task OnDownloadClick()
    {
        UpdateContent = await _updateService.GetUpdateContentAsync(true);
    }

    [RelayCommand]
    private async Task OnLinkClick(LinkClickedEventArgs e)
    {
        if (e.Link.StartsWith("PotatoVN."))
        {
            await DealWithCommand(e.Link);
            return;
        }
        
        await Launcher.LaunchUriAsync(new Uri(e.Link));
    }

}