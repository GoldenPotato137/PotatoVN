using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.ViewModels;

public partial class GalgameSettingViewModel : ObservableRecipient, INavigationAware
{
    public Galgame Gal
    {
        get; set;
    }

    private readonly GalgameCollectionService _galService;
    private readonly INavigationService _navigationService;

    public GalgameSettingViewModel(IDataCollectionService<Galgame> galCollectionService, INavigationService navigationService)
    {
        Gal = new Galgame();
        _galService = (GalgameCollectionService)galCollectionService;
        _navigationService = navigationService;
    }

    public async void OnNavigatedFrom()
    {
        await _galService.SaveGalgamesAsync();
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not Galgame galgame)
        {
            return;
        }

        Gal = galgame;
    }

    [RelayCommand]
    public void OnBack()
    {
        if(_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }

    [RelayCommand]
    public async void OnGetInfoFromRss()
    {
        await _galService.PhraseGalInfoAsync(Gal);
    }
}
