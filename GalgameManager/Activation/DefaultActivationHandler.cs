using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;

namespace GalgameManager.Activation;

public class DefaultActivationHandler : ActivationHandler<List<string>>
{
    private readonly INavigationService _navigationService;

    public DefaultActivationHandler(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    protected override bool CanHandleInternal(List<string> args)
    {
        // None of the ActivationHandlers has handled the activation.
        return _navigationService.Frame?.Content == null;
    }

    protected async override Task HandleInternalAsync(List<string> args)
    {
        if (args.Count == 1)
        {
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
        }
        else //jump list 
        {
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, args[1]);
        }
        await Task.CompletedTask;
    }
}
