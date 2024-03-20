using GalgameManager.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Core.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.ViewModels;
using GalgameManager.Views;
using H.NotifyIcon;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using UnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;
using WindowExtensions = H.NotifyIcon.WindowExtensions;

namespace GalgameManager;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>() where T : class
    {
        if ((Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        return service;
    }
    
    public static T GetResource<T>(string key)
    {
        if (Current.Resources[key] is not T resource)
            throw new ArgumentException($"{key} needs to be registered in Resource.xaml.");
        return resource;
    }

    private static Application _instance = null!;
    public static WindowEx? MainWindow { get; set; }
    public static TaskbarIcon? SystemTray { get; set; }
    
    public static UIElement? AppTitlebar { get; set; }
    public static bool Closing;
    public static event Action? OnAppClosing;
    public static DispatcherQueue DispatcherQueue { get; } = DispatcherQueue.GetForCurrentThread();

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // 启动跳转处理
            // 从前往后依次处理，直到找到能处理的处理器
            // Launch Activation Handlers
            services.AddTransient<IActivationHandler, JumpListActivationHandler>();     // JumpList
            services.AddTransient<IActivationHandler, UpdateContentHandler>();          // 更新内容
            // Protocol Activation Handlers
            services.AddTransient<IActivationHandler, BgmOAuthActivationHandler>();     // BgmOAuth
            // Default Handler
            services.AddTransient<IActivationHandler, DefaultActivationHandler>();      // 启动页

            // Services
            services.AddTransient<INavigationViewService, NavigationViewService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddTransient<IJumpListService, JumpListService>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDataCollectionService<Galgame>, GalgameCollectionService>();
            services.AddSingleton<IDataCollectionService<GalgameFolder>, GalgameFolderCollectionService>();
            services.AddSingleton<IFaqService, FaqService>();
            services.AddSingleton<IFilterService, FilterService>();
            services.AddSingleton<ICategoryService, CategoryService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IAppCenterService, AppCenterService>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<IBgmOAuthService, BgmOAuthService>();
            services.AddSingleton<IInfoService, InfoService>();
            services.AddSingleton<IBgTaskService, BgTaskService>();
            services.AddSingleton<IPvnService, PvnService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<InfoViewModel>();
            services.AddTransient<InfoPage>();
            services.AddTransient<AccountViewModel>();
            services.AddTransient<AccountPage>();
            services.AddTransient<CategoryViewModel>();
            services.AddTransient<CategoryPage>();
            services.AddTransient<CategorySettingViewModel>();
            services.AddTransient<CategorySettingPage>();
            services.AddTransient<HelpViewModel>();
            services.AddTransient<HelpPage>();
            services.AddTransient<GalgameSettingViewModel>();
            services.AddTransient<GalgameSettingPage>();
            services.AddTransient<PlayedTimePage>();
            services.AddTransient<PlayedTimeViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<LibraryPage>();
            services.AddTransient<GalgameFolderViewModel>();
            services.AddTransient<GalgameFolderPage>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<UpdateContentViewModel>();
            services.AddTransient<UpdateContentPage>();
            services.AddTransient<GalgameViewModel>();
            services.AddTransient<HomeDetailPage>();
            services.AddTransient<HomeViewModel>();
            services.AddTransient<HomePage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();
            services.AddTransient<GalgameCharacterPage>();
            services.AddTransient<GalgameCharacterViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        UnhandledException += App_UnhandledException;
        AppInstance.GetCurrent().Activated += OnActivated;
    }

    private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
        GetService<ILocalSettingsService>().SaveSettingAsync(KeyValues.LastError, e.Message + "\n" + e.Exception);
        e.Handled = false;
        SetWindowMode(WindowMode.Close);
    }
    
    /// <summary>
    /// 应用启动入口
    /// </summary>
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        _instance = this;
        await GetService<IActivationService>().LaunchedAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
    }

    private async void OnActivated(object?_, AppActivationArguments arguments)
    {
        await GetService<IActivationService>().HandleActivationAsync(arguments);
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            SetWindowMode(WindowMode.Normal);
        });
    }
    
    /// <summary>
    /// 设置窗口模式<br/>
    /// <para>
    /// 其中最小化到系统托盘的模式以重启代替关闭主窗口，窗口关闭后内存无法释放<br/>
    /// 见：https://github.com/microsoft/microsoft-ui-xaml/issues/9063<br/>
    /// https://github.com/microsoft/microsoft-ui-xaml/issues/7282
    /// </para>
    /// </summary>
    /// <param name="mode"></param>
    public static void SetWindowMode(WindowMode mode)
    {
        switch (mode)
        {
            case WindowMode.Normal:
                GetService<IPageService>().InitAsync();
                WindowExtensions.Show(MainWindow!);
                MainWindow!.Restore();
                MainWindow!.BringToFront();
                if (GetService<ILocalSettingsService>().ReadSettingAsync<string>(KeyValues.LastError)
                        .Result is { } error)
                {
                    GetService<IInfoService>().Event(EventType.AppError, InfoBarSeverity.Error,
                        "App_Error".GetLocalized(), msg: error);
                    GetService<ILocalSettingsService>().RemoveSettingAsync(KeyValues.LastError);
                }
                break;
            case WindowMode.Minimize:
                MainWindow!.Minimize();
                break;
            case WindowMode.SystemTray:
                OnAppClosing?.Invoke();
                GetService<IBgTaskService>().SaveBgTasksString();
                AppInstance.Restart("/r");
                WindowExtensions.Hide(MainWindow!);
                break;
            case WindowMode.Close:
                OnAppClosing?.Invoke();
                Closing = true;
                SystemTray?.Dispose();
                _instance.Exit();
                break;
        }
    }
}
