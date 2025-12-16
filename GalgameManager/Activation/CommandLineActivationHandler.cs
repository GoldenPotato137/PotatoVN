using Windows.ApplicationModel.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.ViewModels;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Activation;

public class CommandLineActivationHandler : ActivationHandler<AppActivationArguments>
{
    private readonly GalgameCollectionService _galgameCollectionService;
    private Galgame? _game;

    public CommandLineActivationHandler(IGalgameCollectionService galgameCollectionService)
    {
        _galgameCollectionService = (galgameCollectionService as GalgameCollectionService)!;
    }

    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.Launch) return false;
        if (args.Data is not ILaunchActivatedEventArgs launchArgs) return false;

        var fullArgs = launchArgs.Arguments.Trim();
        var actualArgs = fullArgs;

        // Extract actual arguments by handling the quoted executable path
        if (fullArgs.StartsWith("\""))
        {
            var closingQuoteIndex = fullArgs.IndexOf('\"', 1);
            if (closingQuoteIndex != -1)
            {
                actualArgs = fullArgs.Substring(closingQuoteIndex + 1).TrimStart();
            }
        }

        if (string.IsNullOrEmpty(actualArgs)) return false;

        try
        {
            var target = actualArgs.Trim();
            // Remove quotes if present
            if (target.StartsWith("\"") && target.EndsWith("\"") && target.Length >= 2)
            {
                target = target[1..^1];
            }

            // Direct UUID check (no /j prefix required)
            if (Guid.TryParse(target, out var guid))
            {
                _game = _galgameCollectionService.GetGalgameFromUuid(guid);
                return _game is not null;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    protected async override Task HandleInternalAsync(AppActivationArguments args)
    {
        App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, new GalgamePageParameter
        {
            Galgame = _game!,
            StartGame = true
        });
        await Task.CompletedTask;
    }
}
