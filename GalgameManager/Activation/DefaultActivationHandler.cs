using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.ViewModels;

namespace GalgameManager.Activation;

public class DefaultActivationHandler : ActivationHandler<List<string>>
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUpdateService _updateService;

    public DefaultActivationHandler(INavigationService navigationService, IUpdateService updateService,
        ILocalSettingsService localSettingsService)
    {
        _navigationService = navigationService;
        _updateService = updateService;
        _localSettingsService = localSettingsService;
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
        else //jump list 
        {
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, new Tuple<string, bool>(args[1], true));
        }
        await Task.CompletedTask;
    }
}
