using GalgameManager.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Services;

public class ActivationService : IActivationService
{
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IUpdateService _updateService;
    private readonly IDataCollectionService<GalgameFolder> _galgameFolderCollectionService;
    private readonly IDataCollectionService<Galgame> _galgameCollectionService;
    private readonly IAppCenterService _appCenterService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthenticationService _authenticationService;
    private UIElement? _shell = null;

    public ActivationService(
        IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService,
        IDataCollectionService<GalgameFolder> galgameFolderCollectionService,
        IDataCollectionService<Galgame> galgameCollectionService,
        IUpdateService updateService, IAppCenterService appCenterService,
        ICategoryService categoryService,
        IAuthenticationService authenticationService)
    {
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _galgameFolderCollectionService = galgameFolderCollectionService;
        _galgameCollectionService = galgameCollectionService;
        _updateService = updateService;
        _appCenterService = appCenterService;
        _categoryService = categoryService;
        _authenticationService = authenticationService;
    }

    public async Task LaunchedAsync(object activationArgs)
    {
        // 多实例启动，切换到第一实例，第一实例 App.OnActivated() 响应
        IList<AppInstance> instances = AppInstance.GetInstances();
        if (instances.Count > 1)
        {
            await instances[0].RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Application.Current.Exit();
            return;
        }
        
        // Execute tasks before activation.
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        //防止有人手快按到页面内容
        App.MainWindow.Content.Visibility = Visibility.Collapsed;

        // Activate the MainWindow.
        App.MainWindow.Activate();

        var result = await _authenticationService.StartAuthentication();
        if (!result)
        {
            Application.Current.Exit();
            return;
        }

        await _galgameCollectionService.InitAsync();
        await _galgameFolderCollectionService.InitAsync();
        await _categoryService.Init();

        //准备好数据后，再呈现页面
        App.MainWindow.Content.Visibility = Visibility.Visible;

        //使窗口重新获得焦点
        App.MainWindow.Activate();

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Execute tasks after activation.
        await StartupAsync();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        await _updateService.UpdateSettingsBadgeAsync();
        await _appCenterService.StartAsync();
    }
}