using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Newtonsoft.Json;

namespace GalgameManager.Models;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public class GalgameFolder
{
    [JsonIgnore] public GalgameCollectionService GalgameService;

    [JsonIgnore] public bool IsRunning;
    [JsonIgnore] public bool IsUnpacking;
    [JsonIgnore] public int ProgressValue;
    [JsonIgnore] public int ProgressMax;
    [JsonIgnore] public string ProgressText = string.Empty;
    private readonly List<Galgame> _galgames = new();
    public event VoidDelegate? ProgressChangedEvent;

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
        await Task.CompletedTask;
        return new ObservableCollection<Galgame>(_galgames);
    }

    /// <summary>
    /// 向库中新增一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public void AddGalgame(Galgame galgame)
    {
        _galgames.Add(galgame);
    }

    /// <summary>
    /// 从库中删除一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public void DeleteGalgame(Galgame galgame)
    {
        _galgames.Remove(galgame);
    }

    /// <summary>
    /// 检查该游戏是否应该在这个库中
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <returns></returns>
    public bool IsInFolder(Galgame galgame)
    {
        if (galgame.CheckExist() == false)
            return false;
        return !string.IsNullOrEmpty(galgame.Path) && IsInFolder(galgame.Path);
    }

    /// <summary>
    /// 检查这个路径的游戏是否应该这个库中
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns></returns>
    public bool IsInFolder(string path)
    {
        return path[..path.LastIndexOf('\\')] == Path ;
    }

    /// <summary>
    /// 从信息源更新库的所有游戏的信息
    /// <param name="toUpdate">要更新的游戏列表，null则为全部更新</param>
    /// </summary>
    public async Task GetInfoFromRss(List<Galgame>? toUpdate = null)
    {
        List<Galgame> galgames = toUpdate ?? (await GetGalgameList()).ToList();
        IsRunning = true;
        for (var i = 0;i<galgames.Count;i++)
        {
            Galgame galgame = galgames[i];
            ProgressText = $"正在获取 {galgame.Name.Value} 的信息, {i}/{galgames.Count}";
            ProgressChangedEvent?.Invoke();
            await GalgameService.PhraseGalInfoAsync(galgame);
        }

        IsRunning = false;
        ProgressChangedEvent?.Invoke();
    }
}
