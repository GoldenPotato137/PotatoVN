using System.Text;
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

        var fullArgs = launchArgs.Arguments;
        if (string.IsNullOrWhiteSpace(fullArgs)) return false;

        // Avoid conflict with JumpListActivationHandler
        if (fullArgs.StartsWith("/j")) return false;

        // Robust parsing of arguments
        var parsedArgs = ParseCommandLine(fullArgs);

        foreach (var arg in parsedArgs)
        {
            // Skip flags (like /detached, -debug, etc.)
            if (arg.StartsWith("/") || arg.StartsWith("-")) continue;

            // Skip likely executable paths (common in Execution Alias scenarios)
            if (arg.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

            // Check if it's a UUID
            if (Guid.TryParse(arg, out var guid))
            {
                _game = _galgameCollectionService.GetGalgameFromUuid(guid);
                if (_game != null) return true;
            }
        }

        return false;
    }

    protected async override Task HandleInternalAsync(AppActivationArguments args)
    {
        App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, new GalgamePageParameter
        {
            Galgame = _game!,
            StartGame = true,
            IsCommandLineLaunch = true
        });
        await Task.CompletedTask;
    }

    private static List<string> ParseCommandLine(string cmdLine)
    {
        var args = new List<string>();
        if (string.IsNullOrWhiteSpace(cmdLine)) return args;

        var currentArg = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < cmdLine.Length; i++)
        {
            char c = cmdLine[i];
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (char.IsWhiteSpace(c) && !inQuote)
            {
                if (currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            }
            else
            {
                currentArg.Append(c);
            }
        }

        if (currentArg.Length > 0) args.Add(currentArg.ToString());

        return args;
    }
}