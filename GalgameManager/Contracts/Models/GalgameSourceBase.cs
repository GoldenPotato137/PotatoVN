using System.Collections;
using System.Collections.ObjectModel;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using Newtonsoft.Json;
using StdPath = System.IO.Path;

namespace GalgameManager.Contracts.Models;

public class GalgameSourceBase
{
    [JsonIgnore] public GalgameCollectionService GalgameService;

    [JsonIgnore] public bool IsRunning;
    private readonly List<Galgame> _galgames = new();

    public string Url => $"{GalgameSourceType.SourceTypeToString()}://{Path}";
    public string Path { get; set; }
    public virtual SourceType GalgameSourceType => throw new NotImplementedException();
    public bool ScanOnStart { get; set; }

    public GalgameSourceBase(string path, IDataCollectionService<Galgame> service)
    {
        Path = path;
        GalgameService = ((GalgameCollectionService?)service)!;
    }

    public async virtual Task<ObservableCollection<Galgame>> GetGalgameList()
    {
        await Task.CompletedTask;
        return new ObservableCollection<Galgame>(_galgames);
    }

    public virtual Galgame GetGalgameByName(string name)
    {
        return _galgames.Where(g => g.Name == name).ToList()[0];
    }

    /// <summary>
    /// 向库中新增一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public virtual void AddGalgame(Galgame galgame)
    {
        _galgames.Add(galgame);
    }

    /// <summary>
    /// 从库中删除一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public virtual void DeleteGalgame(Galgame galgame)
    {
        _galgames.Remove(galgame);
    }

    /// <summary>
    /// 检查该游戏是否应该在这个库中
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <returns></returns>
    public virtual bool IsInSource(Galgame galgame)
    {
        return galgame.GalgameSourceType == GalgameSourceType && !string.IsNullOrEmpty(galgame.Path) && IsInSource(galgame.Path);
    }

    /// <summary>
    /// 检查这个路径的游戏是否应该这个库中
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns></returns>
    public virtual bool IsInSource(string path)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取这个库的日志路径（相对存储根目录）
    /// </summary>
    public virtual string GetLogPath() => StdPath.Combine("Logs", GetLogName());
    
    public virtual string GetLogName() => $"Galgame_{Url.ToBase64().Replace("/", "").Replace("=", "")}.txt";

    public async virtual IAsyncEnumerable<(Galgame?, string)> ScanAllGalgames()
    {
        yield break;
        throw new NotImplementedException();
    }

}

public enum SourceType
{
    UnKnown,
    LocalFolder,
    LocalZip,
    Virtual
}

public static class SourceTypeHelper{
    public static string? SourceTypeToString(this SourceType sourceType)
    {
        return sourceType switch
        {
            SourceType.LocalFolder => "local_folder",
            SourceType.LocalZip => "local_zip",
            SourceType.UnKnown => null,
            _ => null
        };
    }
}
