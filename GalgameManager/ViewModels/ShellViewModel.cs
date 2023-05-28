using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    private bool _isBackEnabled;
    private object? _selected;
    [ObservableProperty] private bool _updateBadgeVisibility;

    public INavigationService NavigationService
    {
        get;
    }

    public INavigationViewService NavigationViewService
    {
        get;
    }

    public bool IsBackEnabled
    {
        get => _isBackEnabled;
        set => SetProperty(ref _isBackEnabled, value);
    }

    public object? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public ShellViewModel(INavigationService navigationService, INavigationViewService navigationViewService,
        IUpdateService updateService)
    {
        updateService.SettingBadgeEvent += result => UpdateBadgeVisibility = result;
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;

        // if (e.SourcePageType == typeof(SettingsPage))
        // {
        //     Selected = NavigationViewService.SettingsItem;
        //     return;
        // }

        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }
}
