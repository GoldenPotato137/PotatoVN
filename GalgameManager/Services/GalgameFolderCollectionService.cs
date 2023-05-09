using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using SharpCompress;

namespace GalgameManager.Services;

public class GalgameFolderCollectionService : IDataCollectionService<GalgameFolder>
{
    private ObservableCollection<GalgameFolder> _galgameFolders = new();
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;

    public GalgameFolderCollectionService(ILocalSettingsService localSettingsService, IDataCollectionService<Galgame> galgameService)
    {
        _localSettingsService = localSettingsService;
        _galgameService = ((GalgameCollectionService?)galgameService)!;
        _galgameService.GalgameAddedEvent += OnGalgameAdded;
    }

    public async Task<ObservableCollection<GalgameFolder>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgameFolders;
    }

    public async Task InitAsync()
    {
        _galgameFolders = await _localSettingsService.ReadSettingAsync<ObservableCollection<GalgameFolder>>(KeyValues.GalgameFolders, true)
                          ?? new ObservableCollection<GalgameFolder>();
        ObservableCollection<Galgame> galgames = await _galgameService.GetContentGridDataAsync();

        await Task.Run(() =>
        {
            foreach (GalgameFolder galgameFolder in _galgameFolders)
            {
                galgameFolder.GalgameService = _galgameService;
                galgames.Where(galgame => galgameFolder.IsInFolder(galgame)).ForEach(galgameFolder.AddGalgame);
            }
        });
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

        GalgameFolder galgameFolder = new(path, _galgameService);
        _galgameFolders.Add(galgameFolder);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgameFolders, _galgameFolders, true);
        if (tryGetGalgame)
        {
            await galgameFolder.GetGalgameInFolder(_localSettingsService);
        }
    }
}

