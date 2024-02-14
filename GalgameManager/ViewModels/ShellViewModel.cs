using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    private bool _isBackEnabled;
    private object? _selected;
    private readonly IBgTaskService _bgTaskService;
    [ObservableProperty] private bool _updateBadgeVisibility;
    [ObservableProperty] private string? _title;
    [ObservableProperty] private bool _infoPageBadgeVisibility;
    [ObservableProperty] private int _infoPageBadgeCount;

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
        IUpdateService updateService, IBgTaskService bgTaskService)
    {
        NavigationService = navigationService;
        NavigationViewService = navigationViewService;
        _bgTaskService = bgTaskService;
        NavigationService.Navigated += OnNavigated;
        updateService.SettingBadgeEvent += result => UpdateBadgeVisibility = result;
        _bgTaskService.BgTaskAdded += _ => UpdateInfoPageBadge();
        _bgTaskService.BgTaskRemoved += _ => UpdateInfoPageBadge();
        
        UpdateInfoPageBadge();
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;

        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }
    
    private void UpdateInfoPageBadge()
    {
        InfoPageBadgeCount = _bgTaskService.GetBgTasks().Count();
        InfoPageBadgeVisibility = _infoPageBadgeCount > 0;
    }
}
