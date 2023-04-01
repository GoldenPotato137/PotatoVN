using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;

using GalgameManager.Core.Contracts.Services;
using GalgameManager.Services;

using Newtonsoft.Json;

namespace GalgameManager.Models;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public class GalgameFolder
{
    [JsonIgnore]
    public GalgameCollectionService GalgameService;
    public string Path
    {
        get;
        set;
    }
    
    public GalgameFolder(string path, IDataCollectionService<Galgame> service)
    {
        Path = path;
        GalgameService = ((GalgameCollectionService?)service)!;
    }
    
    public async Task<ObservableCollection<Galgame>> GetGalgameList()
    {
        var games = await GalgameService.GetContentGridDataAsync();
        return new ObservableCollection<Galgame>(games.Where(g => g.Path.StartsWith(Path)).ToList());;
    }

    public async Task GetGalgameInFolder()
    {
        if (!Directory.Exists(Path)) return ;
        foreach (var subPath in Directory.GetDirectories(Path))
        {
            await GalgameService.TryAddGalgameAsync(subPath);
        }
    }
}
