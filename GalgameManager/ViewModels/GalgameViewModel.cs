using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;

namespace GalgameManager.ViewModels;

public class GalgameViewModel : ObservableRecipient, INavigationAware
{
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private Galgame? _item;

    public Galgame? Item
    {
        get => _item;
        set => SetProperty(ref _item, value);
    }

    private async Task Init(ILocalSettingsService localSettingsService)
    {
        var tmp = await localSettingsService.ReadSettingAsync<string>("AppBackgroundRequestedTheme");
        Console.WriteLine(tmp);
        await Task.CompletedTask;
    }

    public GalgameViewModel(IDataCollectionService<Galgame> dataCollectionService, ILocalSettingsService localSettingsService)
    {
        _dataCollectionService = dataCollectionService;
        Task.Run(()=> Init(localSettingsService));
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is string name)
        {
            var data = await _dataCollectionService.GetContentGridDataAsync();
            Item = data.First(i => i.Name == name);
        }
    }

    public void OnNavigatedFrom()
    {
    }
}
