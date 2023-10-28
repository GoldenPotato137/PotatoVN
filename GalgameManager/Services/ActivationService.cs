using System.Diagnostics;
using Windows.Storage;
using GalgameManager.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
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
    private readonly IBgmOAuthService _bgmOAuthService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    private UIElement? _shell;

    public ActivationService(
        IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService,
        IDataCollectionService<GalgameFolder> galgameFolderCollectionService,
        IDataCollectionService<Galgame> galgameCollectionService,
        IUpdateService updateService, IAppCenterService appCenterService,
        ICategoryService categoryService,IBgmOAuthService bgmOAuthService,
        IAuthenticationService authenticationService, ILocalSettingsService localSettingsService,
        IFilterService filterService)
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
    }

    public async Task LaunchedAsync(object activationArgs)
    {
        // 多实例启动，切换到第一实例，第一实例 App.OnActivated() 响应
        IList<AppInstance> instances = AppInstance.GetInstances();
        if (instances.Count > 1)
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

        await _filterService.InitAsync();
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

    public async Task HandleActivationAsync(object activationArgs)
    {
        IActivationHandler? activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        await _updateService.UpdateSettingsBadgeAsync();
        await _appCenterService.StartAsync();
        await _bgmOAuthService.TryRefreshOAuthAsync();
        await CheckFont();
        await ((GalgameCollectionService)_galgameCollectionService).SyncUpgrade();
        await ((GalgameCollectionService)_galgameCollectionService).SyncGames();
    }

    /// <summary>
    /// 检查字体是否安装，如果没有安装，弹出提示框
    /// </summary>
    private async Task CheckFont()
    {
        if(await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FontInstalled) == false)
        {
            if (Utils.IsFontInstalled("Segoe Fluent Icons") == false)
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = App.MainWindow.Content.XamlRoot,
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
}