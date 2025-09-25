using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts;
using GalgameManager.Helpers;
using LiteDB;
using Newtonsoft.Json;
using StdPath = System.IO.Path;

namespace GalgameManager.Models.Sources;

public abstract partial class GalgameSourceBase : ObservableObject, IDisplayableGameObject
{
    /// 当游戏列表发生变化时触发，第二个bool为true时为删除，否则为添加
    public event Action<Galgame, bool>? GalgamesChanged;
    /// 当监听需求改变时触发，对于各个实现的具体触发时机（比如说具体什么监听目标发生改变）由具体实现决定，应手动triger
    public event Action<GalgameSourceBase>? DetectChanged;
    [BsonId] public Guid Id { get; set; } = Guid.NewGuid();
    [BsonIgnore] [JsonIgnore] public bool IsRunning;
    /// 所有游戏和路径，只用于序列化，任何时候都不应该直接操作这个列表
    [BsonIgnore] public List<GalgameAndPath> Galgames { get; } = new();
    /// 在手动选择要扫描的文件夹时被取消选中的文件夹的路径
    public List<string> DontScanPath { get; set; } = [];
    /// 子库列表；由Service初始化时计算，不在json中存储
    [BsonIgnore] [JsonIgnore] public List<GalgameSourceBase> SubSources { get; } = new();
    /// 父库，若为null则表示这是根库；由Service初始化时计算，不在json中存储
    [BsonIgnore] [JsonIgnore] public GalgameSourceBase? ParentSource { get; set; }

    [BsonIgnore] [JsonIgnore] public string Url => CalcUrl(SourceType, Path);
    public string Path { get; set; } = "";
    [BsonIgnore] [JsonIgnore] public ExUri PathUri => new (Path);
    public abstract GalgameSourceType SourceType { get; }
    [ObservableProperty] private bool _scanOnStart;
    /// 是否能调整ScanOnStart属性
    [JsonIgnore][BsonIgnore] public abstract bool CanChangeScanOnStart { get; }
    /// 是否在启动时检查库和游戏是否存在
    [ObservableProperty] private bool _checkOnStart = true;
    /// 是否能调整CheckOnStart属性
    [JsonIgnore][BsonIgnore] public abstract bool CanChangeCheckOnStart { get; }
    public virtual string? ExtraData { get; set; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private DateTime _lastPlayed = DateTime.MinValue;
    [ObservableProperty] private DateTime _lastClicked = DateTime.MinValue;
    /// 是否对库进行监听总开关
    [ObservableProperty] private bool _detect;
    [ObservableProperty] private bool _detectFolderAdd;
    [ObservableProperty] private bool _detectFolderRemove = true;
    /// 是否可以调整Detect（监听）属性
    [JsonIgnore][BsonIgnore] public abstract bool CanChangeDetect { get; }
    /// 是否保存meta备份（包括meta.json和封面图）
    [ObservableProperty] private bool _saveMetaBackup = false;
    /// 是否可以调整SaveMetaBackup属性
    [JsonIgnore][BsonIgnore] public abstract bool CanChangeSaveMetaBackup { get; }
    /// 是否可以手动往这个库里添加游戏
    [JsonIgnore][BsonIgnore] public abstract bool IsGameAddable { get; }
    /// 是否可以扫描这个库
    [JsonIgnore][BsonIgnore] public abstract bool IsSourceScanable { get; }
    /// 是否可以删除这个库
    [JsonIgnore][BsonIgnore] public abstract bool IsDelectable { get; }
    
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
        
        // 通知父源游戏列表变化
        NotifyParentSourcesChanged(galgame, false);
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
        
        // 通知父源游戏列表变化
        NotifyParentSourcesChanged(galgame, true);
    }

    /// <summary>
    /// 通知所有父源游戏列表变化
    /// </summary>
    /// <param name="galgame">发生变化的游戏</param>
    /// <param name="isRemoved">是否为删除操作</param>
    private void NotifyParentSourcesChanged(Galgame galgame, bool isRemoved)
    {
        var parent = ParentSource;
        while (parent != null)
        {
            parent.GalgamesChanged?.Invoke(galgame, isRemoved);
            parent = parent.ParentSource;
        }
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

    /// <summary>
    /// 获取这个库的子源列表（包括自己与子库的子库等）
    /// </summary>
    /// <returns></returns>
    public List<GalgameSourceBase> GetSubSourcesRecursive()
    {
        List<GalgameSourceBase> result = [];
        Dfs(this);
        return result;

        void Dfs(GalgameSourceBase current)
        {
            result.Add(current);
            foreach(GalgameSourceBase subSource in current.SubSources) 
                Dfs(subSource);
        }
    }

    /// <summary>
    /// 获取这个库的所有子库，这个库本身，以及所有祖先库
    /// </summary>
    /// <returns></returns>
    public List<GalgameSourceBase> GetSubAncestorsSources()
    {
        List<GalgameSourceBase> result = GetSubSourcesRecursive();
        GalgameSourceBase? current = ParentSource;
        while (current is not null)
        {
            result.Add(current);
            current = current.ParentSource;
        }
        return result;
    }

    public async virtual IAsyncEnumerable<(string? path, string msg)> ScanAllGalgames()
    {
        await Task.CompletedTask;
        yield break;
    }

    public abstract bool ApplySearchKey(string searchKey);

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
    
    public void UpdateLastPlayed() => LastPlayed = Galgames.Select(g => g.Galgame.LastPlayTime).Max();

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDetectChanged(bool value) => DetectChanged?.Invoke(this);

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDetectFolderAddChanged(bool value) => DetectChanged?.Invoke(this);

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnDetectFolderRemoveChanged(bool value) => DetectChanged?.Invoke(this);

    /// <summary>
    /// 以此库当前的DontScanPath列表更新孩子的该属性与其父亲、祖先的该属性 <br/>
    /// 孩子们的DontScanPath会被覆盖，祖先的会被合并（不动不属于这个库的路径）
    /// </summary>
    public void UpdateDontScanPath()
    {
        ExUri currentPath = PathUri;
        foreach (GalgameSourceBase sub in SubSources)
        {
            sub.DontScanPath.Clear();
            ExUri subPath = sub.PathUri;
            foreach (var p in DontScanPath.Where(p => subPath.IsAncestorOf(new ExUri(p))))
                sub.DontScanPath.Add(p);
        }
        GalgameSourceBase? current = ParentSource;
        while (current is not null)
        {
            // 先移除所有属于当前库的路径再添加
            List<string> toRemove = [];
            toRemove.AddRange(current.DontScanPath.Where(p => currentPath.IsAncestorOf(new ExUri(p))));
            foreach (var p in toRemove) current.DontScanPath.Remove(p);
            current.DontScanPath.AddRange(DontScanPath);
            current = current.ParentSource;
        }
    }
}

// 新增Source Type时，请务必实现：
// 1. GalgameSourceConverter里的ReadJson
// 2. 其对应的SourceService类（如：LocalFolderSourceService），并把它注册到App里（依赖注入）
// 3. SourceServiceFactory里获取service相关语句
// 4. 添加GalgameSourceType_xxx字样的本地化翻译
// 5. GalgameSourceCollecctionService:
//  5.1 实现AddSourceAsync方法
//  5.2 CheckGamesInSourceAsync方法
//  5.3 GetGalgameSource
//  5.4 GetSourcePath
// 6. AddSourceDialog.xaml.cs: 添加对应的SourceTypeComboBox选项，以及相关的检查逻辑
// 7. 如果这个库能够扫描游戏，则需要重载galgameSourceBase的ScanAllGalgames方法来获取可能的游戏路径
// 8. SourceTypeHelper（本文件）的两个方法
public enum GalgameSourceType
{
    UnKnown,
    LocalFolder,
    LocalZip,
    Virtual,
    Steam,
}

public enum GalgameSourceSortKeys
{
    Name,
    LastPlay,
    LastClick,
    Path,
    SourceType,
    GalgameCount,
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
            GalgameSourceType.Steam => "steam",
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
            "steam" => GalgameSourceType.Steam,
            _ => GalgameSourceType.UnKnown
        };
    }
}
