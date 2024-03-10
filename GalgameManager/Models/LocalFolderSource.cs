using System.Collections.ObjectModel;
using GalgameManager.Contracts;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Newtonsoft.Json;
using StdPath = System.IO.Path;

namespace GalgameManager.Models;

public class LocalFolderSource: IGalgameSource
{
    public SourceType GetSourceType() => SourceType.LocalFolder;

    [JsonIgnore] public GalgameCollectionService GalgameService;

    [JsonIgnore] public bool IsRunning { get; set; }
    [JsonIgnore] public bool IsUnpacking { get; set; }
    private readonly List<Galgame> _galgames = new();

    public string Path { get; set; }
    public string Url { get; set; }
    public bool ScanOnStart { get; set; }

    public LocalFolderSource(string url, IDataCollectionService<Galgame> service)
    {
        if (url[..url.IndexOf("://", StringComparison.Ordinal)] != "local") throw new Exception();
        Url = url;
        Path = url[url.IndexOf("://", StringComparison.Ordinal)..];
        GalgameService = ((GalgameCollectionService?)service)!;
    }

    public string GetUrl() => Url;

    public async Task<ObservableCollection<Galgame>> GetGalgameList()
    {
        await Task.CompletedTask;
        return new ObservableCollection<Galgame>(_galgames);
    }

    public Galgame GetGalgameByName(string name)
    {
        return _galgames.Where(g => g.Name == name).ToList()[0];
    }

    /// <summary>
    /// 向库中新增一个游戏, 用于初始化
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
    public bool IsInSource(Galgame galgame)
    {
        if (galgame.CheckExist() == false)
            return false;
        return !string.IsNullOrEmpty(galgame.Path) && IsInSource(galgame.Path);
    }

    /// <summary>
    /// 检查这个路径的游戏是否应该这个库中
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns></returns>
    public bool IsInSource(string path)
    {
        return path[..path.LastIndexOf('\\')] == Path ;
    }

    /// <summary>
    /// 获取这个库的日志路径（相对存储根目录）
    /// </summary>
    public string GetLogPath() => StdPath.Combine("Logs", $"GalgameFolder_{Url.ToBase64()}.txt");
    
    public string GetLogName() => $"GalgameFolder_{Url.ToBase64()}.txt";
}
