using GalgameManager.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<List<string>> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IDataCollectionService<GalgameFolder> _galgameFolderCollectionService;
    private readonly IDataCollectionService<Galgame> _galgameCollectionService;
    private UIElement? _shell = null;

    public ActivationService(ActivationHandler<List<string>> defaultHandler,
        IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService,
        IDataCollectionService<GalgameFolder> galgameFolderCollectionService,
        IDataCollectionService<Galgame> galgameCollectionService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _galgameFolderCollectionService = galgameFolderCollectionService;
        _galgameCollectionService = galgameCollectionService;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.MainWindow.Activate();

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

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await _galgameCollectionService.InitAsync();
        await _galgameFolderCollectionService.InitAsync();
        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        await Task.CompletedTask;
    }
}