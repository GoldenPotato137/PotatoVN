using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts;
using GalgameManager.Helpers;
using LiteDB;
using Newtonsoft.Json;
using StdPath = System.IO.Path;

namespace GalgameManager.Models.Sources;

public partial class GalgameSourceBase : ObservableObject, IDisplayableGameObject
{
    /// 当游戏列表发生变化时触发，第二个bool为true时为删除，否则为添加
    public event Action<Galgame, bool>? GalgamesChanged;
    /// 当监听需求改变时触发，对于各个实现的具体触发时机（比如说具体什么监听目标发生改变）由具体实现决定，应手动triger
    public event Action<GalgameSourceBase>? DetectChanged;
    [BsonId] public Guid Id { get; set; } = Guid.NewGuid();
    [BsonIgnore] [JsonIgnore] public bool IsRunning;
    /// 所有游戏和路径，只用于序列化，任何时候都不应该直接操作这个列表
    [BsonIgnore] public List<GalgameAndPath> Galgames { get; } = new();
    /// 子库列表；由Service初始化时计算，不在json中存储
    [BsonIgnore] [JsonIgnore] public List<GalgameSourceBase> SubSources { get; } = new();
    /// 父库，若为null则表示这是根库；由Service初始化时计算，不在json中存储
    [BsonIgnore] [JsonIgnore] public GalgameSourceBase? ParentSource { get; set; }

    [BsonIgnore] [JsonIgnore] public string Url => CalcUrl(SourceType, Path);
    public string Path { get; set; } = "";
    public virtual GalgameSourceType SourceType => throw new NotImplementedException();
    [ObservableProperty] private bool _scanOnStart;
    public virtual string? ExtraData { get; set; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private DateTime _lastPlayed = DateTime.MinValue;
    [ObservableProperty] private DateTime _lastClicked = DateTime.MinValue;
    /// 是否对库进行监听总开关
    [ObservableProperty] private bool _detect;
    [ObservableProperty] private bool _detectFolderAdd;
    [ObservableProperty] private bool _detectFolderRemove = true;
    
    # region LITEDB_MAPPING
    [JsonIgnore] public List<GalgameAndPathDbDto> GalgamesDto
    {
        get => Galgames.Select(t => new GalgameAndPathDbDto(t.Galgame.Uuid, t.Path)).ToList();
        set => _galgamesDto = value;
    }
    public List<GalgameAndPathDbDto> GetLoadedGalgames() => _galgamesDto;
    private List<GalgameAndPathDbDto> _galgamesDto = [];
    # endregion
    
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
        GalgamesChanged?.Invoke(galgame, false);
    }

    /// <summary>
    /// 从库中删除一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public virtual void DeleteGalgame(Galgame galgame)
    {
        Galgames.RemoveAll(g => g.Galgame == galgame);
        galgame.Sources.Remove(this);
        GalgamesChanged?.Invoke(galgame, true);
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
    
    public virtual string GetLogName() => $"Source_{Name.RemoveInvalidChars()}.txt";

    public async virtual IAsyncEnumerable<(string? path, string msg)> ScanAllGalgames()
    {
        await Task.CompletedTask;
        yield break;
    }

    public virtual bool ApplySearchKey(string searchKey)
    {
        return Path.ContainX(searchKey);
    }

    public override string ToString() => Name;

    /// 是否可以手动往这个库里添加游戏
    public virtual bool IsGameAddable => false;
    
    /// 是否可以扫描这个库
    public virtual bool IsSourceScanable => false;
    
    /// 是否可以删除这个库
    public virtual bool IsDelectable => true;

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
    
    public void UpdateLastPlayed() => LastPlayed = Galgames.Select(g => g.Galgame.LastPlayTime).Max();

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDetectChanged(bool value) => DetectChanged?.Invoke(this);

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDetectFolderAddChanged(bool value) => DetectChanged?.Invoke(this);

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDetectFolderRemoveChanged(bool value) => DetectChanged?.Invoke(this);
}

public enum GalgameSourceType
{
    UnKnown,
    LocalFolder,
    LocalZip,
    Virtual,
}

public static class SourceTypeHelper
{
    public static string? SourceTypeToString(this GalgameSourceType sourceType)
    {
        return sourceType switch
        {
            GalgameSourceType.LocalFolder => "local_folder",
            GalgameSourceType.LocalZip => "local_zip",
            GalgameSourceType.Virtual => "virtual",
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
            "virtual" => GalgameSourceType.Virtual,
            _ => GalgameSourceType.UnKnown
        };
    }
}
