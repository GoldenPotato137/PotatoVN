using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;

namespace GalgameManager.ViewModels;

public class GalgameFolderViewModel : ObservableObject, INavigationAware
{
    private readonly IDataCollectionService<GalgameFolder> _dataCollectionService;
    private GalgameFolder? _item;
    public ObservableCollection<Galgame> Galgames = new();

    public GalgameFolder? Item
    {
        get => _item;

        set
        {
            SetProperty(ref _item, value);
            if (value != null)
                Galgames = value.GetGalgameList().Result;
        }
    }

    public GalgameFolderViewModel(IDataCollectionService<GalgameFolder> dataCollectionService)
    {
        _dataCollectionService = dataCollectionService;
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is string path)
        {
            var data = await _dataCollectionService.GetContentGridDataAsync();
            Item = data.First(i => i.Path == path);
        }
    }

    public void OnNavigatedFrom()
    {
    }
}
