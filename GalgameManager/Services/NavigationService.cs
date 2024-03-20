using System.Diagnostics.CodeAnalysis;

using CommunityToolkit.WinUI.UI.Animations;

using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly IPageService _pageService;
    private object? _lastParameterUsed;
    private Frame? _frame;
    private bool _isMemoryImprove;
    private (string pageKey, object? param, bool clearNavigation)? _lastFailedNavigation;

    public event NavigatedEventHandler? Navigated;

    public Frame? Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.MainWindow!.Content as Frame;
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    [MemberNotNullWhen(true, nameof(Frame), nameof(_frame))]
    public bool CanGoBack => Frame != null && Frame.CanGoBack;

    public NavigationService(IPageService pageService, ILocalSettingsService localSettingsService)
    {
        _pageService = pageService;
        _pageService.OnInit += () =>
        {
            if (_lastFailedNavigation is null) return;
            NavigateTo(_lastFailedNavigation.Value.pageKey, _lastFailedNavigation.Value.param,
                _lastFailedNavigation.Value.clearNavigation);
        };
        localSettingsService.OnSettingChanged += OnSettingChanged;
    }

    private void OnSettingChanged(string key, object? value)
    {
        if (key == KeyValues.MemoryImprove)
            _isMemoryImprove = value is true;
    }

    private void RegisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated += OnNavigated;
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
        }
    }

    public bool GoBack()
    {
        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            _frame.GoBack();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            return true;
        }

        return false;
    }

    public bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false)
    {
        var pageType = _pageService.GetPageType(pageKey);

        if (_frame != null && (_frame.Content?.GetType() != pageType || (parameter != null && !parameter.Equals(_lastParameterUsed))))
        {
            _frame.Tag = clearNavigation;
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var navigated = _frame.Navigate(pageType, parameter);
            if (navigated)
            {
                _lastParameterUsed = parameter;
                if (vmBeforeNavigation is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom();
                }
                if(_isMemoryImprove)
                    GC.Collect(); //临时解决切换界面时内存不释放的问题（见：https://github.com/microsoft/microsoft-ui-xaml/issues/5978）
            }

            return navigated;
        }

        _lastFailedNavigation = (pageKey, parameter, clearNavigation);
        return false;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is Frame frame)
        {
            var clearNavigation = (bool)frame.Tag;
            if (clearNavigation)
            {
                frame.BackStack.Clear();
            }

            if (frame.GetPageViewModel() is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedTo(e.Parameter);
            }

            Navigated?.Invoke(sender, e);
        }
    }

    public void SetListDataItemForNextConnectedAnimation(object item) => Frame.SetListDataItemForNextConnectedAnimation(item);
}
