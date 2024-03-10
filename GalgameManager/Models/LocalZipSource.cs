using System.Collections.ObjectModel;
using GalgameManager.Contracts;
using GalgameManager.Enums;
using GalgameManager.Helpers;

using StdPath = System.IO.Path;

namespace GalgameManager.Models;

public class LocalZipSource: IGalgameSource
{
    public SourceType GetSourceType() => SourceType.LocalZip;

    private readonly List<Galgame> _galgames = new();

    public string Path { get; set; }

    public string Url { get; set; }

    public LocalZipSource(string url)
    {
        if (url[..url.IndexOf("://", StringComparison.Ordinal)] != "local_zip") throw new Exception();
        Url = url;
        Path = url[url.IndexOf("://", StringComparison.Ordinal)..];
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

    public bool IsRunning { get; set; }
    public bool IsUnpacking { get; set; }
    public bool ScanOnStart { get; set; }

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
    
    public bool IsInSource(Galgame galgame)
    {
        if (galgame.GameType == GameType.Zip)
            return false;
        return !string.IsNullOrEmpty(galgame.ZipPath) && IsInSource(galgame.ZipPath);
    }

    /// <summary>
    /// 检查这个路径的游戏是否应该这个库中
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns></returns>
    public bool IsInSource(string path)
    {
        return path[..path.LastIndexOf('\\')] == Url ;
    }
    
    /// <summary>
    /// 获取这个库的日志路径（相对存储根目录）
    /// </summary>
    public string GetLogPath() => StdPath.Combine("Logs", $"GalgameSource_{Url.ToBase64()}.txt");
    
    public string GetLogName() => $"GalgameSource_{Url.ToBase64()}.txt";
}