using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.ViewModels;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Activation;


public class DefaultActivationHandler : ActivationHandler<AppActivationArguments>
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;

    public DefaultActivationHandler(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
    }

    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        return true;
    }

    protected async override Task HandleInternalAsync(AppActivationArguments args)
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
