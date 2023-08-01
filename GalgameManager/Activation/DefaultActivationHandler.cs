using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Activation;


public class DefaultActivationHandler : ActivationHandler<AppActivationArguments>
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUpdateService _updateService;
    private readonly IBgmOAuthService _bgmOAuthService;

    public DefaultActivationHandler(INavigationService navigationService, IUpdateService updateService,
        ILocalSettingsService localSettingsService, IBgmOAuthService bgmOAuthService)
    {
        _navigationService = navigationService;
        _updateService = updateService;
        _localSettingsService = localSettingsService;
        _bgmOAuthService = bgmOAuthService;
    }

    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        // None of the ActivationHandlers has handled the activation.
        // return _navigationService.Frame?.Content == null;
        return true;
    }

    protected async override Task HandleInternalAsync(AppActivationArguments args)
    {
        switch (args.Kind)
        {
            case ExtendedActivationKind.Launch:
                List<string> cmlArgs = Environment.GetCommandLineArgs().ToList();
                if (cmlArgs.Count == 1)
                {
                    if (_updateService.ShouldDisplayUpdateContent())
                        _navigationService.NavigateTo(typeof(UpdateContentViewModel).FullName!, true);
                    else
                    {
                        PageEnum page = await _localSettingsService.ReadSettingAsync<PageEnum>(KeyValues.StartPage);
                        switch (page)
                        {
                            case PageEnum.Category:
                                _navigationService.NavigateTo(typeof(CategoryViewModel).FullName!);
                                break;
                            case PageEnum.Home:
                                _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
                                break;
                        }
                    }
                }
                else if (cmlArgs.Count == 3)
                {
                    switch (cmlArgs[1])
                    {
                        case "/j":
                            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, new Tuple<string, bool>(cmlArgs[2], true));
                            break;
                        case "/p":
                            // 理论上会通过 case ExtendedActivationKind.Protocol
                            // 以防万一
                            await _bgmOAuthService.FinishOAuthWithUri(cmlArgs[2]);
                            break;
                    }
                }
                break;
            case ExtendedActivationKind.Protocol:
                await _bgmOAuthService.FinishOAuthWithUri((args.Data as ProtocolActivatedEventArgs)!.Uri.ToString());
                break;
        }
        await Task.CompletedTask;
    }
}
