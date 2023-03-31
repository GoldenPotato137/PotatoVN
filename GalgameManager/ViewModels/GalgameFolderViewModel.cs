using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using CommunityToolkit.Mvvm.ComponentModel;

using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public class GalgameFolderViewModel : ObservableObject, INavigationAware
{
    private readonly IDataCollectionService<GalgameFolder> _dataCollectionService;
    private GalgameFolder? _item;
    public ObservableCollection<Galgame> Galgames = new();

    public GalgameFolder? Item
    {
        get => _item;

        private set
        {
            SetProperty(ref _item, value);
            if (value != null)
                Galgames = value.GetGalgameList().Result;
        }
    }

    public GalgameFolderViewModel(IDataCollectionService<GalgameFolder> dataCollectionService, IDataCollectionService<Galgame> galgameService)
    {
        _dataCollectionService = dataCollectionService;
        var galgameService1 = (GalgameCollectionService) galgameService;
        galgameService1.GalgameAddedEvent += ReloadGalgameList;
    }

    private void ReloadGalgameList(Galgame galgame)
    {
        if (_item == null) return;
        if (galgame.Path.StartsWith(_item.Path))
            Galgames.Add(galgame);
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
