using System.Collections.ObjectModel;

using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
// ReSharper disable EnforceForeachStatementBraces

namespace GalgameManager.Services;

public class GalgameFolderCollectionService : IDataCollectionService<GalgameFolder>
{
    private readonly ObservableCollection<GalgameFolder> _galgameFolders;
    private readonly IDataCollectionService<Galgame> _galCollectionService;
    private ILocalSettingsService LocalSettingsService { get; }
    
    public GalgameFolderCollectionService(ILocalSettingsService localSettingsService, IDataCollectionService<Galgame> galCollectionService)
    {
        LocalSettingsService = localSettingsService;
        _galCollectionService = galCollectionService;
        
        _galgameFolders = new()
        {
            new GalgameFolder(@"D:\Game", _galCollectionService),
            new GalgameFolder(@"D:\GalGame", _galCollectionService)
        };
    }

    public async Task<ObservableCollection<GalgameFolder>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgameFolders;
    }
}
