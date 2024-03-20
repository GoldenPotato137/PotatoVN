using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class UpdateContentViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private string _updateContent = string.Empty;
    private readonly IUpdateService _updateService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly GalgameCollectionService _galgameCollectionService;
    [ObservableProperty] private Visibility _isDownloading = Visibility.Collapsed;
    [ObservableProperty] private Visibility _displayTitle = Visibility.Collapsed;
    [ObservableProperty] private string _currentVersion =
        "UpdateContentPage_CurrentVersion".GetLocalized() + RuntimeHelper.GetVersion();

    public UpdateContentViewModel(IUpdateService updateService,ILocalSettingsService localSettingsService,
        IDataCollectionService<Galgame> galgameService)
    {
        _updateService = updateService;
        _galgameCollectionService = (GalgameCollectionService)galgameService;
        _localSettingsService = localSettingsService;
        localSettingsService.OnSettingChanged += OnSettingChanged;
        _updateService.DownloadEvent += () => IsDownloading = Visibility.Visible;
        _updateService.DownloadCompletedEvent += () =>
        {
            IsDownloading = Visibility.Collapsed;
            _ = DisplayMsgAsync("UpdateContentPage_Download_Success".GetLocalized(), InfoBarSeverity.Success);
        };
        _updateService.DownloadFailedEvent += async s =>
        {
            await DisplayMsgAsync(s, InfoBarSeverity.Error);
        };
    }

    private async void OnSettingChanged(string key, object? value)
    {
        switch (key)
        {
            case KeyValues.UploadData:
                if (value is true)
                    await DisplayMsgAsync("UpdateContentPage_AppCenterSuccess".GetLocalized(), InfoBarSeverity.Success);
                break;
        }
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
            case "PotatoVN.CategoryNow":
                await App.GetService<ICategoryService>().UpdateAllGames();
                _ = DisplayMsgAsync("UpdateContentPage_CategoryNow".GetLocalized(), InfoBarSeverity.Success);
                break;
            case "PotatoVN.SyncFromBgm":
                _ = DisplayMsgAsync("HomePage_Downloading".GetLocalized(), InfoBarSeverity.Informational,1000 * 120);
                (GalStatusSyncResult, string) result = await _galgameCollectionService.DownloadAllPlayStatus(RssType.Bangumi);
                await DisplayMsgAsync(result.Item2, result.Item1.ToInfoBarSeverity());
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

    #region INFOBAR_CTRL
    
    [ObservableProperty] private string _infoBarMsg = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _infoBarOpen;
    private int _infoBarIndex;

    private async Task DisplayMsgAsync(string msg, InfoBarSeverity severity, int delayMs = 3000)
    {
        var index = ++_infoBarIndex;
        InfoBarMsg = msg;
        InfoBarSeverity = severity;
        InfoBarOpen = true;
        await Task.Delay(delayMs);
        if (index == _infoBarIndex)
            InfoBarOpen = false;
    }

    #endregion

}