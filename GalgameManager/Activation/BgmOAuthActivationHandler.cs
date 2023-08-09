using Windows.ApplicationModel.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Activation;

public class BgmOAuthActivationHandler : ActivationHandler<AppActivationArguments>
{
    private Uri? _uri;
    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.Protocol) return false;
        _uri = (args.Data as ProtocolActivatedEventArgs)!.Uri;
        return _uri.Host == BgmOAuthConfig.Host;
    }

    protected async override Task HandleInternalAsync(AppActivationArguments args)
    {
        await App.GetService<IBgmOAuthService>().FinishOAuthWithUri(_uri!);
    }
}