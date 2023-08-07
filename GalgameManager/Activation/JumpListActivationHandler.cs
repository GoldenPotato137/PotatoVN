using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Activation;

public class JumpListActivationHandler : ActivationHandler<AppActivationArguments>
{
    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.Launch) return false;
        List<string> cmlArgs = Environment.GetCommandLineArgs().ToList();
        if (cmlArgs.Count != 3) return false; //"GalgameManager.exe" "/j" "galgamePath"
        return cmlArgs[1] == "/j";
    }

    protected async override Task HandleInternalAsync(AppActivationArguments args)
    {
        List<string> cmlArgs = Environment.GetCommandLineArgs().ToList();
        App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, new Tuple<string, bool>(cmlArgs[2], true));
        await Task.CompletedTask;
    }
}