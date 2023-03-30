using System.Collections.ObjectModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;

namespace GalgameManager.ViewModels;

public class LibraryViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<GalgameFolder> _dataCollectionService;
    public ObservableCollection<GalgameFolder> Source { get; private set; } = new();
    public ICommand ItemClickCommand { get; }
    public LibraryViewModel(INavigationService navigationService, IDataCollectionService<GalgameFolder> dataCollectionService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;

        ItemClickCommand = new RelayCommand<GalgameFolder>(OnItemClick);
    }

    public async void OnNavigatedTo(object parameter)
    {
        Source = await _dataCollectionService.GetContentGridDataAsync();
    }

    public void OnNavigatedFrom(){}
    
    private void OnItemClick(GalgameFolder? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            _navigationService.NavigateTo(typeof(GalgameFolderViewModel).FullName!, clickedItem.Path);
        }
    }
}
