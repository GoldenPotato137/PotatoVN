using System.Collections.ObjectModel;

using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
// ReSharper disable EnforceForeachStatementBraces

namespace GalgameManager.Services;

// ReSharper disable EnforceIfStatementBraces
// This class holds sample data used by some generated pages to show how they can be used.
// TODO: The following classes have been created to display sample data. Delete these files once your app is using real data.
// 1. Contracts/Services/ISampleDataService.cs
// 2. Services/SampleDataService.cs
// 3. Models/SampleCompany.cs
// 4. Models/SampleOrder.cs
// 5. Models/SampleOrderDetail.cs
public class GalgameCollectionService : IDataCollectionService<Galgame>
{
    private ObservableCollection<Galgame> _galgames = new(); 
    private ObservableCollection<GalgameFolder> _galgameFolders = new();
    private ILocalSettingsService LocalSettingsService { get; }
    
    public GalgameCollectionService(ILocalSettingsService localSettingsService)
    {
        LocalSettingsService = localSettingsService;
        GetGalgames();
    }

    private async void GetGalgames()
    {
        _galgames = new();
        _galgames.Add(new Galgame(@"D:\Game\魔女的夜宴"));
        _galgames.Add(new Galgame(@"D:\Game\星光咖啡馆与死神之蝶"));
        _galgames.Add(new Galgame(@"D:\Game\大图书馆的牧羊人"));

        foreach (var galgame in await GetGalFromFolder(@"D:\GalGame"))
            _galgames.Add(galgame);

        for(var i=0;i<_galgames.Count;i++)
        {
            _galgames[i] = await PhraserAsync(_galgames[i], new BgmPhraser(LocalSettingsService));
        }
    }
    
    private static async Task<Galgame> PhraserAsync(Galgame galgame, IGalInfoPhraser phraser)
    {
        var tmp = await phraser.GetGalgameInfo(galgame.Name);
        if (tmp == null) return galgame;

        galgame.Description = tmp.Description;
        galgame.Developer = tmp.Developer;
        galgame.Name = tmp.Name;
        return galgame;

    }
    
    public async Task<ObservableCollection<Galgame>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgames;
    }

    private static async Task<List<Galgame>> GetGalFromFolder(string folder)
    {
        List<Galgame> result = new();
        if (!Directory.Exists(folder)) return result;
        await Task.Run(() =>
        {
            result.AddRange(Directory.GetDirectories(folder).Select(subFolder => new Galgame(subFolder)));
        });
        return result;
    }
}
