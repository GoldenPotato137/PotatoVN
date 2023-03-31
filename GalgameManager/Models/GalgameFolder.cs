using System.Collections.ObjectModel;

using GalgameManager.Core.Contracts.Services;
using GalgameManager.Services;

namespace GalgameManager.Models;

public class GalgameFolder
{
    public GalgameCollectionService Service;
    public string Path
    {
        get;
        set;
    }
    
    public GalgameFolder(string path, IDataCollectionService<Galgame> service)
    {
        Path = path;
        Service = ((GalgameCollectionService?)service)!;
    }
    
    public async Task<ObservableCollection<Galgame>> GetGalgameList()
    {
        var games = await Service.GetContentGridDataAsync();
        return new ObservableCollection<Galgame>(games.Where(g => g.Path.StartsWith(Path)).ToList());;
    }
}
