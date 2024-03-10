using System.Collections.ObjectModel;
using GalgameManager.Contracts;
using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Models;

public class GalgameZipSource: IGalgameSource
{
    public GalgameZipProtocol Protocol;
    public string Url;
    private readonly List<Galgame> _galgames = new();


    public GalgameZipSource(string url)
    {
        Url = url;
        switch (url[..url.IndexOf("://", StringComparison.Ordinal)])
        {
            case "local":
                Protocol = GalgameZipProtocol.Local;
                break;
            default:
                throw new NotImplementedException();
        }
    }
    
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
    public string GetLogPath() => Path.Combine("Logs", $"GalgameFolder_{Url.ToBase64()}.txt");
    
    public string GetLogName() => $"GalgameFolder_{Url.ToBase64()}.txt";
}