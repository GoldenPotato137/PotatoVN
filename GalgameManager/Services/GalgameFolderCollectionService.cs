using System.Collections.ObjectModel;

using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
// ReSharper disable EnforceForeachStatementBraces

namespace GalgameManager.Services;

public class GalgameFolderCollectionService : IDataCollectionService<GalgameFolder>
{
    private readonly ObservableCollection<GalgameFolder> _galgameFolders;
    private ILocalSettingsService LocalSettingsService { get; }
    
    public GalgameFolderCollectionService(ILocalSettingsService localSettingsService)
    {
        LocalSettingsService = localSettingsService;
        
        _galgameFolders = new()
        {
            new GalgameFolder(@"D:\Game"),
            new GalgameFolder(@"D:\GalGame")
        };
    }

    public async Task<ObservableCollection<GalgameFolder>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgameFolders;
    }
}
