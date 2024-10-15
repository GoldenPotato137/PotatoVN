using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts;
using GalgameManager.Helpers;
using Newtonsoft.Json;
using StdPath = System.IO.Path;

namespace GalgameManager.Models.Sources;

public partial class GalgameSourceBase : ObservableObject, IDisplayableGameObject
{
    [JsonIgnore] public bool IsRunning;
    /// 所有游戏和路径，只用于序列化，任何时候都不应该直接操作这个列表
    public List<GalgameAndPath> Galgames { get; } = new();
    /// 子库列表；由Service初始化时计算，不在json中存储
    [JsonIgnore] public List<GalgameSourceBase> SubSources { get; } = new();
    /// 父库，若为null则表示这是根库；由Service初始化时计算，不在json中存储
    [JsonIgnore] public GalgameSourceBase? ParentSource { get; set; }

    public string Url => CalcUrl(SourceType, Path);
    public string Path { get; set; } = "";
    public virtual GalgameSourceType SourceType => throw new NotImplementedException();
    public bool ScanOnStart { get; set; }
    [ObservableProperty] private string _name = string.Empty;
    
    public static string CalcUrl(GalgameSourceType type, string path) => $"{type.SourceTypeToString()}://{path}";

    public static (GalgameSourceType type, string path) ResolveUrl(string url)
    {
        if(!url.Contains("://")) throw new PvnException("illegal url: missing '://'");
        var parts = url.Split("://");
        return (parts[0].ToEnum() , parts[1]);
    }

    public GalgameSourceBase(string path)
    {
        Path = path;
        SetNameFromPath();
    }

    public GalgameSourceBase()
    {
    }

    public IEnumerable<Galgame> GetGalgameList() => Galgames.Select(g => g.Galgame);

    /// <summary>
    /// 检查这个库是否包含某个游戏
    /// </summary>
    public bool Contain(Galgame? galgame) => Galgames.Exists(g => g.Galgame == galgame);

    public virtual Galgame GetGalgameByName(string name)
    {
        return Galgames.Where(g => g.Galgame.Name == name).ToList()[0].Galgame;
    }
    
    /// 获取游戏在这个库中的路径，若游戏不在库中则返回null
    public string? GetPath(Galgame game) => Galgames.Find(g => g.Galgame == game)?.Path;

    /// <summary>
    /// 向库中新增一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <param name="path">路径</param>
    public virtual void AddGalgame(Galgame galgame, string path)
    {
        Galgames.Add(new GalgameAndPath(galgame, path));
        galgame.Sources.Add(this);
    }

    /// <summary>
    /// 从库中删除一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public virtual void DeleteGalgame(Galgame galgame)
    {
        Galgames.RemoveAll(g => g.Galgame == galgame);
        galgame.Sources.Remove(this);
    }

    /// <summary>
    /// 检查该游戏是否应该在这个库中
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <returns></returns>
    public virtual bool IsInSource(Galgame galgame)
    {
        return galgame.SourceType == SourceType && !string.IsNullOrEmpty(galgame.Path) && IsInSource(galgame.Path);
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
        await Task.CompletedTask;
        yield break;
    }

    public virtual bool ApplySearchKey(string searchKey)
    {
        return Path.ContainX(searchKey);
    }

    public override string ToString() => Name;

    public void SetNameFromPath()
    {
        try
        {
            DirectoryInfo info = new(Path);
            Name = info.Name;
        }
        catch (Exception) //Path too long
        {
            //ignore
        }
    }

}

public enum GalgameSourceType
{
    UnKnown,
    LocalFolder,
    LocalZip,
}

public static class SourceTypeHelper
{
    public static string? SourceTypeToString(this GalgameSourceType sourceType)
    {
        return sourceType switch
        {
            GalgameSourceType.LocalFolder => "local_folder",
            GalgameSourceType.LocalZip => "local_zip",
            GalgameSourceType.UnKnown => null,
            _ => null
        };
    }
    
    public static GalgameSourceType ToEnum(this string sourceType)
    {
        return sourceType switch
        {
            "local_folder" => GalgameSourceType.LocalFolder,
            "local_zip" => GalgameSourceType.LocalZip,
            _ => GalgameSourceType.UnKnown
        };
    }
}
