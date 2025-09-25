using System.Diagnostics;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using GalgameManager.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Views;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Services;

public class ActivationService : IActivationService
{
    private readonly IEnumerable<IActivationHandler> _activationHandlers;//
    private readonly IThemeSelectorService _themeSelectorService; //
    private readonly IUpdateService _updateService;
    private readonly IGalgameSourceCollectionService _galgameFolderCollectionService;
    private readonly IGalgameCollectionService _galgameCollectionService;
    private readonly IAppCenterService _appCenterService;
    private readonly ICategoryService _categoryService;
    private readonly IStaffService _staffService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IBgmOAuthService _bgmOAuthService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    private readonly IPageService _pageService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IPvnService _pvnService;
    private readonly IPluginService _pluginService;
    private readonly IInfoService _infoService;
    
    public ActivationService(
        IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService,
        IGalgameSourceCollectionService galgameFolderCollectionService,
        IGalgameCollectionService galgameCollectionService,
        IUpdateService updateService, IAppCenterService appCenterService,
        ICategoryService categoryService,IBgmOAuthService bgmOAuthService,
        IAuthenticationService authenticationService, ILocalSettingsService localSettingsService,
        IFilterService filterService, IPageService pageService, IBgTaskService bgTaskService, IPvnService pvnService,
        IInfoService infoService, IStaffService staffService, IPluginService pluginService)
    {
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _galgameFolderCollectionService = galgameFolderCollectionService;
        _galgameCollectionService = galgameCollectionService;
        _updateService = updateService;
        _appCenterService = appCenterService;
        _categoryService = categoryService;
        _bgmOAuthService = bgmOAuthService;
        _authenticationService = authenticationService;
        _localSettingsService = localSettingsService;
        _filterService = filterService;
        _pageService = pageService;
        _bgTaskService = bgTaskService;
        _pvnService = pvnService;
        _infoService = infoService;
        _staffService = staffService;
        _pluginService = pluginService;
    }

    public async Task LaunchedAsync(object activationArgs)
    {
        // 多实例启动，切换到第一实例，第一实例 App.OnActivated() 响应
        IList<AppInstance> instances = AppInstance.GetInstances();
        if (instances.Count > 1 && AppInstance.GetCurrent() != instances[0])
        {
            if (activationArgs is AppActivationArguments args)
            {
                await instances[0].RedirectActivationToAsync(args);
            }
            Application.Current.Exit();
            return;
        }
        
        // Execute tasks before activation.
        await InitializeAsync();

        if (IsRestart() == false)
        {
            var result = await _authenticationService.StartAuthentication();
            if (!result)
            {
                Application.Current.Exit();
                return;
            } 
        }

        ImportWindow? importWindow = null;
        if (await CheckImport() is { } import)
        {
            importWindow = new ImportWindow(import, _localSettingsService);
            importWindow.Activate();
            await importWindow.Import();
        }

        try
        {
            await Task.Run(async () =>
            {
                _localSettingsService.InitDatabase();
                await _galgameCollectionService.InitAsync();
                await _galgameFolderCollectionService.InitAsync();
                await _categoryService.Init();
                await _staffService.InitAsync();
                await _filterService.InitAsync();
            });
            importWindow?.Close();
        }
        catch (IOException) //数据库正在被占用
        {
            Application.Current.Exit();
            return;
        }
        catch (Exception e)
        {
            if (importWindow is not null)
                await importWindow.Restore(e);
            else
            {
                var backup = await _localSettingsService.BackupFailedDataAsync();
                await _localSettingsService.SaveSettingAsync(KeyValues.LastError,
                    $"{"ActivationService_LoadDataError".GetLocalized(backup)} {e.Message}");
            }
            AppInstance.Restart("/safemode"); //safemode并没有实现，只是为了和正常的启动参数区分开
            return;
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        if (IsRestart())
        {
            await _bgTaskService.ResolvedBgTasksAsync();
        }
        
        App.Status = WindowMode.SystemTray;

        // Execute tasks after activation.
        await StartupAsync(activationArgs);
    }

    public async Task HandleActivationAsync(object activationArgs)
    {
        IActivationHandler? activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await UiThreadInvokeHelper.InvokeAsync(async Task() =>
            {
                await activationHandler.HandleAsync(activationArgs);
            });
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        UiDefaultValues.Init();

        // 初始化窗口
        if (IsRestart() == false)
        {
            await _pageService.InitAsync();
            //防止有人手快按到页面内容
            App.MainWindow!.Content.Visibility = Visibility.Collapsed;
        }
        
        //系统托盘
        App.GetResource<XamlUICommand>("SetWindowNormalCommand").ExecuteRequested += (_, _) =>
        {
            App.SetWindowMode(WindowMode.Normal);
        };
        App.GetResource<XamlUICommand>("CloseAppCommand").ExecuteRequested += (_, _) =>
        {
            App.SetWindowMode(WindowMode.Close);
        };
        App.SystemTray = App.GetResource<TaskbarIcon>("TrayIcon");
        App.SystemTray.ForceCreate(false);
    }

    private async Task StartupAsync(object activationArgs)
    {
        await _galgameCollectionService.StartAsync();
        await _galgameFolderCollectionService.StartAsync();
        var activateWindow = !IsRestart();
        if (activationArgs is AppActivationArguments { Kind: ExtendedActivationKind.StartupTask })
            activateWindow = !await _localSettingsService.ReadSettingAsync<bool>(KeyValues.MinToTrayWhenAutoStart);
        if (activateWindow) App.SetWindowMode(WindowMode.Normal);
        await _pluginService.InitAsync();
        if (IsRestart() == false) _pvnService.Startup();
        if (IsRestart() == false) await _updateService.UpdateSettingsBadgeAsync();
        await _appCenterService.StartAsync();
        if(IsRestart() == false) await _bgmOAuthService.Init();
        await CheckFont();
    }

    /// <summary>
    /// 检查字体是否安装，如果没有安装，弹出提示框
    /// </summary>
    private async Task CheckFont()
    {
        if (IsRestart()) return;
        if(await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FontInstalled) == false)
        {
            if (Utils.IsFontInstalled("Segoe Fluent Icons") == false)
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = App.MainWindow!.Content.XamlRoot,
                    RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
                    Title = "ActivationService_FontPopup_Title".GetLocalized(),
                    PrimaryButtonText = "Yes".GetLocalized(),
                    CloseButtonText = "Cancel".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary
                };
                StackPanel stackPanel = new()
                {
                    Spacing = 20
                };
                TextBlock textBlock = new()
                {
                    Text = "ActivationService_FontPopup_Msg".GetLocalized()
                };
                CheckBox checkBox = new()
                {
                    Content = "ActivationService_FontPopup_NoLongerDisplay".GetLocalized()
                };
                stackPanel.Children.Add(textBlock);
                stackPanel.Children.Add(checkBox);
                dialog.Content = stackPanel;
                dialog.PrimaryButtonClick += async (_, _) =>
                {
                    StorageFile? file = await StorageFile.GetFileFromApplicationUriAsync
                        (new Uri("ms-appx:///Assets/Fonts/Segoe Fluent Icons.ttf"));
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = file.Path,
                        UseShellExecute = true,
                    });
                };
                dialog.CloseButtonClick += async (_, _) =>
                {
                    await _localSettingsService.SaveSettingAsync(KeyValues.FontInstalled, checkBox.IsChecked);
                };
                
                await dialog.ShowAsync();
            }
            
            if (Utils.IsFontInstalled("Segoe Fluent Icons"))
                await _localSettingsService.SaveSettingAsync(KeyValues.FontInstalled, true);
        }
    }

    /// <summary>
    /// 判断是否是重启（aka进入系统托盘模式）<br/>
    /// 关于为什么要以重启进入系统托盘模式，见：<see cref="App.SetWindowMode"/>
    /// </summary>
    public static bool IsRestart()
    {
        AppActivationArguments activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        ExtendedActivationKind kind = activatedArgs.Kind;

        if (kind == ExtendedActivationKind.Launch)
        {
            if (activatedArgs.Data is ILaunchActivatedEventArgs launchArgs)
            {
                var argStrings = launchArgs.Arguments.Split();
                if (argStrings.Length > 1)
                    argStrings = argStrings.Skip(1).ToArray();

                return Array.Exists(argStrings, str => str == "/r");
            }
        }
        return false;
    }

    /// <summary>
    /// 检查数据根目录下是否有导入压缩包
    /// </summary>
    private Task<FileInfo?> CheckImport()
    {
        if (!_localSettingsService.LocalFolder.Exists) return Task.FromResult<FileInfo?>(null);
        FileInfo? zip = _localSettingsService.LocalFolder.GetFiles().FirstOrDefault(item =>
            item.Name.EndsWith(".pvnExport.zip"));
        return Task.FromResult(zip); // 给后面异步改造预留接口
    }
}