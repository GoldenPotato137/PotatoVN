using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;

namespace GalgameManager.ViewModels;

public class UpdateContentViewModel : ObservableObject, INavigationAware
{
    public string Content = "#114514\n123456\n#1919810\n123456";
    private readonly IUpdateService _updateService;

    public UpdateContentViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    public async void OnNavigatedTo(object parameter)
    {
        Content = await _updateService.GetUpdateContentAsync();
    }

    public void OnNavigatedFrom()
    {
        
    }
}