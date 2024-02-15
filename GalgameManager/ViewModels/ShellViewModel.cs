using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    private bool _isBackEnabled;
    private object? _selected;
    private bool _isDeveloperMode;
    private int _unreadInfos;
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
        IUpdateService updateService, IBgTaskService bgTaskService, ILocalSettingsService localSettingsService,
        IInfoService infoService)
    {
        NavigationService = navigationService;
        NavigationViewService = navigationViewService;
        _bgTaskService = bgTaskService;

        _isDeveloperMode = localSettingsService.ReadSettingAsync<bool>(KeyValues.DevelopmentMode).Result;
        
        NavigationService.Navigated += OnNavigated;
        updateService.SettingBadgeEvent += result => UpdateBadgeVisibility = result;
        _bgTaskService.BgTaskAdded += _ => UpdateInfoPageBadge();
        _bgTaskService.BgTaskRemoved += _ => UpdateInfoPageBadge();
        infoService.Infos.CollectionChanged += (_, _) =>
        {
            if(_isDeveloperMode == false) return;
            _unreadInfos = infoService.Infos.Count(info =>
                info.Severity is InfoBarSeverity.Warning or InfoBarSeverity.Error && info.Read == false);
            UpdateInfoPageBadge();
        };
        localSettingsService.OnSettingChanged += HandleLocalSettingChanged;
        
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

    private void HandleLocalSettingChanged(string key, object value)
    {
        if(key == KeyValues.DevelopmentMode)
            _isDeveloperMode = (bool)value;
    }
    
    private void UpdateInfoPageBadge()
    {
        InfoPageBadgeCount = _bgTaskService.GetBgTasks().Count() + _unreadInfos;
        InfoPageBadgeVisibility = _infoPageBadgeCount > 0;
    }
}
