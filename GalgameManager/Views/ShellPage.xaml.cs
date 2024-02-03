using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.ViewModels;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel
    {
        get;
    }

    private readonly ILocalSettingsService _localSettingsService;

    public ShellPage(ShellViewModel viewModel, ILocalSettingsService localSettingsService)
    {
        ViewModel = viewModel;
        _localSettingsService = localSettingsService;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);

        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow!.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
        App.MainWindow.AppWindow.Closing += MainWindowOnClosed;
        AppTitleBarText.Text = "AppDisplayName".GetLocalized();
    }

    private void MainWindowOnClosed(AppWindow appWindow, AppWindowClosingEventArgs appWindowClosingEventArgs)
    {
        if(App.Closing) return;
        WindowMode closeMode = _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.CloseMode).Result;
        if (closeMode == WindowMode.Close) return;
        if (closeMode == WindowMode.Normal)
        {
            appWindowClosingEventArgs.Cancel = true;
            _ = CloseConfirm();
        }
        else
        {
            appWindowClosingEventArgs.Cancel = true;
            App.SetWindowMode(closeMode);
        }
    }

    private async Task CloseConfirm()
    {
        CloseConfirmDialog dialog = new();
        await dialog.ShowAsync();
        if (dialog.RememberMe)
            await _localSettingsService.SaveSettingAsync(KeyValues.CloseMode, dialog.Result);
        App.SetWindowMode(dialog.Result);
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
        
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var resource = args.WindowActivationState == WindowActivationState.Deactivated ? "WindowCaptionForegroundDisabled" : "WindowCaptionForeground";

        AppTitleBarText.Foreground = (SolidColorBrush)Application.Current.Resources[resource];
        App.AppTitlebar = AppTitleBarText;
    }

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }

    private void NavigationViewControl_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties? properties = e.GetCurrentPoint(sender as UIElement).Properties;
        if(properties.IsXButton1Pressed)
            App.GetService<INavigationService>().GoBack();
    }
}
