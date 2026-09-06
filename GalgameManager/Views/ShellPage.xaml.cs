using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using GalgameManager.Views.Dialog;
using GalgameManager.WinApp.Base.Models.Msgs;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualBasic;
using Windows.Storage;
using Windows.System;

namespace GalgameManager.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel
    {
        get;
    }

    private readonly ILocalSettingsService _localSettingsService;
    private readonly ISidebarService _sidebarService;
    private readonly Dictionary<string, NavigationViewItem> _builtInNavItems = new();
    private readonly List<NavigationViewItem> _pluginMenuItems = []; //所有放在menu的插件按钮
    private readonly List<NavigationViewItem> _pluginFooterItems = []; //所有放在footer的插件按钮
    private readonly bool _isSegoeFluentIconsInstalled = Utils.IsFontInstalled("Segoe Fluent Icons");

    public ShellPage(ShellViewModel viewModel, ILocalSettingsService localSettingsService, ISidebarService sidebarService)
    {
        ViewModel = viewModel;
        _localSettingsService = localSettingsService;
        _sidebarService = sidebarService;
        InitializeComponent();
        AutomationProperties.SetAutomationId(NavigationViewControl, "AppNavigation");
        AutomationProperties.SetAutomationId(HomeNavItem, "HomeNavItem");
        AutomationProperties.SetAutomationId(LibraryNavItem, "LibraryNavItem");
        AutomationProperties.SetAutomationId(CategoryNavItem, "CategoryNavItem");

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
        NavigationViewControl.ItemInvoked += NavigationViewControl_OnItemInvoked;
        InitializeSidebarButtonMap();
        _sidebarService.ButtonsChanged += RefreshSidebarButtons;
        _localSettingsService.OnSettingChanged += BackgroundSettingChangedHandle;
        ViewModel.NavigationService.Navigated += OnNavigated;
        RefreshSidebarButtons();

        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow!.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
        App.MainWindow.AppWindow.Closing += MainWindowOnClosed;
        AppTitleBarText.Text = "AppDisplayName".GetLocalized();
        return;

        void InitializeSidebarButtonMap()
        {
            _builtInNavItems[SidebarButtonIds.MultiStream] = MultiStreamNavItem;
            _builtInNavItems[SidebarButtonIds.Home] = HomeNavItem;
            _builtInNavItems[SidebarButtonIds.Library] = LibraryNavItem;
            _builtInNavItems[SidebarButtonIds.Category] = CategoryNavItem;
            _builtInNavItems[SidebarButtonIds.AnnualReport] = AnnualReportNavItem;
            _builtInNavItems[SidebarButtonIds.Info] = InfoNavItem;
            _builtInNavItems[SidebarButtonIds.Help] = HelpNavItem;
            _builtInNavItems[SidebarButtonIds.Account] = AccountNavItem;
            _builtInNavItems[SidebarButtonIds.Plugin] = PluginNavItem;
            _builtInNavItems[SidebarButtonIds.Settings] = SettingsNavItem;
        }
    }

    private void MainWindowOnClosed(AppWindow appWindow, AppWindowClosingEventArgs appWindowClosingEventArgs)
    {
        if (App.Status == WindowMode.Close) return;
        if (AppStoragePaths.IsUpgradeUiTest)
        {
            App.SetWindowMode(WindowMode.Close);
            return;
        }
        WindowMode closeMode = _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.CloseMode).Result;
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

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));

        NavigationViewControl.AddHandler(PointerPressedEvent,
            new PointerEventHandler(NavigationViewControl_OnPointerPressed), false);

        await RefreshBackgroundAsync();
        //横幅图真，刷新加载横幅图
        WeakReferenceMessenger.Default.Register<GalgamePlayedMessage>
            (
            this, async (sender, e) =>
            {
                var isCustom = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.CustomBackgroundEnabled);
                if (!isCustom) { await RefreshBackgroundAsync(); }
            }
            );
    }

    //图片模式选择
    public static void ApplyStretchMode(BackgroundStretchMode mode)
    {
        var shell = App.MainWindow?.Content as ShellPage;
        if (shell == null) return;

        var stretch = mode switch
        {
            BackgroundStretchMode.UniformToFill => Stretch.UniformToFill,
            BackgroundStretchMode.Fill => Stretch.Fill,
            BackgroundStretchMode.None => Stretch.None,
            _ => Stretch.Uniform,
        };

        shell.CustomBackgroundBrush.Stretch = stretch;
        shell.CustomBackgroundBrush.AlignmentX = mode == BackgroundStretchMode.None ? AlignmentX.Center : AlignmentX.Left;
        shell.CustomBackgroundBrush.AlignmentY = mode == BackgroundStretchMode.None ? AlignmentY.Center : AlignmentY.Top;
    }

    private bool _wasHomeDetailPage;

    private async void OnNavigated(object sender, NavigationEventArgs e)
    {
        var isHome = ViewModel.NavigationService.Frame?.Content is HomeDetailPage;
        if (isHome == _wasHomeDetailPage) return;
        _wasHomeDetailPage = isHome;
        await RefreshBackgroundAsync();
    }

    private async void BackgroundSettingChangedHandle(string key, object? value)
    {
        switch (key)
        {
            case KeyValues.UseBannerAsBackground:
            case KeyValues.CustomBackgroundEnabled:
                await RefreshBackgroundAsync();
                break;
            case KeyValues.BackgroundStretchMode:
                if (value is BackgroundStretchMode mode)
                    ApplyStretchMode(mode);
                break;
        }
    }

    /// <summary>
    /// 刷新图像,优先级：自定义 > 横幅 > 无
    /// </summary>
    /// <returns></returns>
    public async Task RefreshBackgroundAsync()
    {
        if (ViewModel.NavigationService.Frame?.Content is HomeDetailPage)
        {
            CustomBackgroundBrush.ImageSource = null;
            return;
        }

        var _backgroundStretchMode = _localSettingsService.ReadSettingAsync<BackgroundStretchMode>(KeyValues.BackgroundStretchMode).Result;
        var useBanner = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.UseBannerAsBackground);
        var isCustom = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.CustomBackgroundEnabled);
        if (isCustom)
        {
            await LoadCustomImageAsync();
            ApplyStretchMode(_backgroundStretchMode);
            return;
        }

        if (useBanner)
        {
            var galgameservice = App.GetService<IGalgameCollectionService>();
            if (galgameservice.Galgames.Count == 0)
            {
                await Task.Delay(300);
                await RefreshBackgroundAsync();
                return;
            }
            await LoadBannerAsync();
            ApplyStretchMode(_backgroundStretchMode);
        }
        else { CustomBackgroundBrush.ImageSource = null; }
    }

    private string? _lastBannerPath;
    private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? _cachedBannerBitmap;

    // 加载最后启动游戏的横幅图（仅横幅，不用封面，封面其实也可以实现，但是太糊了，还是算了吧）
    private async Task LoadBannerAsync()
    {
        try
        {
            var galgameservice = App.GetService<IGalgameCollectionService>();
            var LastGame = galgameservice.Galgames.Where(g => g.LastPlayTime > DateTime.MinValue).MaxBy(g => g.LastPlayTime);
            var bannerPath = LastGame?.HeaderImagePath.Value;
            if (string.IsNullOrEmpty(bannerPath) || !System.IO.File.Exists(bannerPath))
            {
                _lastBannerPath = null;
                CustomBackgroundBrush.ImageSource = null;
                return;
            }

            if (bannerPath == _lastBannerPath && _cachedBannerBitmap != null)
            {
                CustomBackgroundBrush.ImageSource = _cachedBannerBitmap;
                return;
            }

            _cachedBannerBitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using var stream = System.IO.File.OpenRead(bannerPath);
            await _cachedBannerBitmap.SetSourceAsync(stream.AsRandomAccessStream());
            CustomBackgroundBrush.ImageSource = _cachedBannerBitmap;
            _lastBannerPath = bannerPath;
        }
        catch
        {
            _lastBannerPath = null;
            CustomBackgroundBrush.ImageSource = null;
        }
    }

    //图片选择器
    public static async Task SetCustomBackgroundAsync(string imagePath)
    {
        var shell = App.MainWindow?.Content as ShellPage;
        if (shell == null) return;

        try
        {
            var folder = await FileHelper.GetFolderAsync(FileHelper.FolderType.Background);
            var destFile = await folder.CreateFileAsync("custom.jpg", CreationCollisionOption.ReplaceExisting);
            var srcFile = await StorageFile.GetFileFromPathAsync(imagePath);
            await srcFile.CopyAndReplaceAsync(destFile);

            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using var stream = await destFile.OpenReadAsync();
            await bitmap.SetSourceAsync(stream);
            shell.CustomBackgroundBrush.ImageSource = bitmap;

            await shell._localSettingsService.SaveSettingAsync(KeyValues.CustomBackgroundEnabled, true);
        }
        catch { }
    }

    //加载图像
    private async Task LoadCustomImageAsync()
    {
        try
        {
            var folder = await FileHelper.GetFolderAsync(FileHelper.FolderType.Background);
            var file = await folder.GetFileAsync("custom.jpg");
            if (file != null)
            {
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                using var stream = await file.OpenReadAsync();
                await bitmap.SetSourceAsync(stream);
                CustomBackgroundBrush.ImageSource = bitmap;
            }
        }
        catch { }
    }

    //清理图片
    public static async Task ClearCustomBackgroundAsync()
    {
        var shell = App.MainWindow?.Content as ShellPage;
        if (shell == null)
        {
            return;
        }
        try
        {
            var folder = await FileHelper.GetFolderAsync(FileHelper.FolderType.Background);
            var file = await folder.TryGetItemAsync("custom.jpg");
            if (file != null)
            {
                await file.DeleteAsync();
            }
        }
        catch { }

        await shell._localSettingsService.SaveSettingAsync(KeyValues.CustomBackgroundEnabled, false);
        await shell.RefreshBackgroundAsync();

    }


    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            AppTitleBarText.Opacity = 0.6;
            AppTitleBarIcon.Opacity = 0.6;
        }
        else
        {
            AppTitleBarText.Opacity = 1.0;
            AppTitleBarIcon.Opacity = 1.0;
        }
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
        if (properties.IsXButton1Pressed)
            App.GetService<INavigationService>().GoBack();
    }

    private async void NavigationViewControl_OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is not string uniqueId) return; //对于内置按钮没有设置tag，这个tag是插件按钮用的
        if (_sidebarService.IsPluginButton(uniqueId) == false) return;
        await _sidebarService.InvokeButtonAsync(uniqueId);
    }

    private void RefreshSidebarButtons()
    {
        Dictionary<string, SidebarButton> buttonMap =
            _sidebarService.GetButtons().ToDictionary(button => button.UniqueId);
        foreach ((var uniqueId, NavigationViewItem navItem) in _builtInNavItems)
        {
            var visible = buttonMap.GetValueOrDefault(uniqueId)?.IsVisible ?? true;
            navItem.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        ClearPluginItems();
        foreach (SidebarButton button in _sidebarService.GetButtons()
                     .Where(button => button is { IsPlugin: true, IsVisible: true, Placement: WinApp.Base.Models.Plugin.SidebarButtonPlacement.Menu })
                     .OrderBy(button => button.Order))
            AddPluginButton(button, NavigationViewControl.MenuItems, 4 + _pluginMenuItems.Count, _pluginMenuItems);

        foreach (SidebarButton button in _sidebarService.GetButtons()
                     .Where(button => button is { IsPlugin: true, IsVisible: true, Placement: WinApp.Base.Models.Plugin.SidebarButtonPlacement.Footer })
                     .OrderBy(button => button.Order))
            AddPluginButton(button, NavigationViewControl.FooterMenuItems,
                NavigationViewControl.FooterMenuItems.IndexOf(SettingsNavItem), _pluginFooterItems);
    }

    private void ClearPluginItems()
    {
        foreach (NavigationViewItem item in _pluginMenuItems)
            NavigationViewControl.MenuItems.Remove(item);
        foreach (NavigationViewItem item in _pluginFooterItems)
            NavigationViewControl.FooterMenuItems.Remove(item);
        _pluginMenuItems.Clear();
        _pluginFooterItems.Clear();
    }

    private void AddPluginButton(SidebarButton button, IList<object> collection, int index,
        ICollection<NavigationViewItem> trackingCollection)
    {
        NavigationViewItem item = new()
        {
            Width = 70,
            Tag = button.UniqueId,
            Content = BuildSidebarButtonContent(),
        };
        collection.Insert(index, item);
        trackingCollection.Add(item);
        return;

        Grid BuildSidebarButtonContent()
        {
            Grid grid = new()
            {
                Width = 60,
                Height = 65,
                Margin = new Thickness(-12, 0, -20, 0),
            };

            grid.Children.Add(new FontIcon
            {
                FontFamily = new FontFamily(_isSegoeFluentIconsInstalled && string.IsNullOrWhiteSpace(button.FluentGlyph) == false
                    ? "Segoe Fluent Icons"
                    : "Segoe MDL2 Assets"),
                Glyph = NormalizeGlyph(_isSegoeFluentIconsInstalled && string.IsNullOrWhiteSpace(button.FluentGlyph) == false
                    ? button.FluentGlyph
                    : button.FallbackGlyph),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0),
            });

            grid.Children.Add(new TextBlock
            {
                Text = button.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 0, 0, 16),
            });

            return grid;
        }

        static string NormalizeGlyph(string? glyph)
        {
            if (string.IsNullOrWhiteSpace(glyph))
                return "\uE10F";
            if (glyph.StartsWith("&#x", StringComparison.OrdinalIgnoreCase) && glyph.EndsWith(';'))
            {
                var hex = glyph[3..^1];
                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
                    return char.ConvertFromUtf32(codePoint);
            }
            return glyph;
        }
    }
}
