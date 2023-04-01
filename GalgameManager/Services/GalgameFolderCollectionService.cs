using System.Collections.ObjectModel;

using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
// ReSharper disable EnforceIfStatementBraces

// ReSharper disable EnforceForeachStatementBraces

namespace GalgameManager.Services;

public class GalgameFolderCollectionService : IDataCollectionService<GalgameFolder>
{
    private readonly ObservableCollection<GalgameFolder> _galgameFolders;
    private readonly GalgameCollectionService _galgameService;

    private ILocalSettingsService LocalSettingsService
    {
        get;
    }

    public GalgameFolderCollectionService(ILocalSettingsService localSettingsService, IDataCollectionService<Galgame> galgameService)
    {
        LocalSettingsService = localSettingsService;
        _galgameService = ((GalgameCollectionService?)galgameService)!;
        _galgameService.GalgameAddedEvent += OnGalgameAdded;

        _galgameFolders = localSettingsService.ReadSettingAsync<ObservableCollection<GalgameFolder>>(KeyValues.GalgameFolders, true).Result
                          ?? new ObservableCollection<GalgameFolder>();

        foreach (var folder in _galgameFolders)
            folder.GalgameService = _galgameService;
    }

    public async Task<ObservableCollection<GalgameFolder>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgameFolders;
    }

    private async void OnGalgameAdded(Galgame galgame)
    {
        try
        {
            await AddGalgameFolderAsync(galgame.Path[..galgame.Path.LastIndexOf('\\')], false);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    /// <summary>
    /// 试图添加一个galgame库
    /// </summary>
    /// <param name="path">库路径</param>
    /// <param name="tryGetGalgame">是否自动寻找库里游戏</param>
    /// <exception cref="Exception">库已经添加过了</exception>
    public async Task AddGalgameFolderAsync(string path, bool tryGetGalgame = true)
    {
        if (_galgameFolders.Any(galFolder => galFolder.Path == path))
        {
            throw new Exception($"这个galgame库{path}已经添加过了");
        }

        var galgameFolder = new GalgameFolder(path, _galgameService);
        _galgameFolders.Add(galgameFolder);
        await LocalSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameFolders, true);
        if(tryGetGalgame)
            await galgameFolder.GetGalgameInFolder();
    }
}

