using Windows.ApplicationModel.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Activation;

public class JumpListActivationHandler : ActivationHandler<AppActivationArguments>
{
    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.Launch) return false;
        if (args.Data is not LaunchActivatedEventArgs arg) return false;
        return arg.Arguments.StartsWith("/j") && arg.Arguments.Length > 2;
    }

    protected async override Task HandleInternalAsync(AppActivationArguments args)
    {
        var target = (args.Data as LaunchActivatedEventArgs)!.Arguments[3..]; //去掉/j与空格
        target = target.Substring(1, target.Length - 2);
        App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, new Tuple<string, bool>(target, true));
        await Task.CompletedTask;
    }
}