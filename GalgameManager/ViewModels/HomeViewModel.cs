using System.Collections.ObjectModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;

namespace GalgameManager.ViewModels;

public class HomeViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;

    public ICommand ItemClickCommand
    {
        get;
    }

    public ObservableCollection<Galgame> Source { get; private set; } = new();

    public HomeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> dataCollectionService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;

        ItemClickCommand = new RelayCommand<Galgame>(OnItemClick);
    }

    public async void OnNavigatedTo(object parameter)
    {
        Source = await _dataCollectionService.GetContentGridDataAsync();
        // Source.Clear();
        //
        // // TODO: Replace with real data.
        // var data = await _dataCollectionService.GetContentGridDataAsync();
        // foreach (var item in data)
        // {
        //     Source.Add(item);
        // }
    }

    public void OnNavigatedFrom()
    {
    }

    private void OnItemClick(Galgame? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, clickedItem.Name);
        }
    }
}
