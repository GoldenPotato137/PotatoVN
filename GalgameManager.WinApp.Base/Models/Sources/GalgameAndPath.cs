using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using LiteDB;
using Newtonsoft.Json;

namespace GalgameManager.Models.Sources;

/// <summary>
/// 游戏在某个库中的条目。若所属库实现<see cref="ILocalGalgameSource"/>，
/// 此条目同时表示一个本地安装实例。
/// </summary>
public partial class GalgameAndPath : ObservableObject
{
    public Guid EntryId { get; set; } = Guid.NewGuid(); // 库内游戏条目的稳定唯一标识
    public Galgame Galgame { get; set; } // 条目关联的逻辑游戏
    [ObservableProperty] private string _path; // 游戏在所属库中的路径
    [ObservableProperty] private LocalInstallationConfig? _localConfig; // 本地安装配置，非本地库条目为null
    [JsonIgnore][BsonIgnore] public GalgameSourceBase? Source { get; internal set; } // 条目所属的游戏库
    [JsonIgnore][BsonIgnore] 
    public List<RssType> RssTypes { get; set; } = []; // 库详情页可选的信息源类型

    /// <summary>
    /// 当前条目是否为本地安装实例。
    /// </summary>
    [JsonIgnore][BsonIgnore]
    public bool IsLocalInstallation => Source is ILocalGalgameSource;

    /// <summary>
    /// 由库名和安装路径组成的显示名称。
    /// </summary>
    [JsonIgnore][BsonIgnore]
    public string DisplayName => Source is null ? Path : $"{Source.Name} · {Path}";

    /// <summary>
    /// 当前条目是否为所属游戏的首选安装实例。
    /// </summary>
    [JsonIgnore][BsonIgnore]
    public bool IsPreferred => Galgame.PreferredInstallationId == EntryId;

    /// <summary>
    /// 初始化一个库内游戏条目。
    /// </summary>
    /// <param name="game">关联的逻辑游戏</param>
    /// <param name="path">游戏在库中的路径</param>
    /// <param name="source">所属游戏库</param>
    /// <param name="entryId">已有条目Id；为null时自动生成</param>
    /// <param name="localConfig">本地安装配置</param>
    public GalgameAndPath(Galgame game, string path, GalgameSourceBase? source = null,
        Guid? entryId = null, LocalInstallationConfig? localConfig = null)
    {
        Galgame = game;
        _path = path;
        Source = source;
        EntryId = entryId ?? Guid.NewGuid();
        LocalConfig = localConfig;
        if (IsLocalInstallation)
            LocalConfig ??= new LocalInstallationConfig();
    }

    /// <summary>
    /// 创建供反序列化使用的空条目。
    /// </summary>
    public GalgameAndPath()
    {
        Galgame = null!;
        _path = string.Empty;
    }

    // 提供用于排序的原生类型快照，避免 ACV 比较 LockableProperty 时出错
    [JsonIgnore][BsonIgnore]
    public string NameForSort => Galgame.Name.Value ?? string.Empty;
    [JsonIgnore][BsonIgnore]
    public DateTime LastPlayTimeForSort => Galgame.LastPlayTime;
    [JsonIgnore][BsonIgnore]
    public string DeveloperForSort => Galgame.Developer.Value ?? string.Empty;
    [JsonIgnore][BsonIgnore]
    public float RatingForSort => Galgame.Rating is { } r && r.Value is float rv ? rv : 0f;
    [JsonIgnore][BsonIgnore]
    public DateTime ReleaseDateForSort => Galgame.ReleaseDate is { } d && d.Value is DateTime dv ? dv : DateTime.MinValue;
    [JsonIgnore][BsonIgnore]
    public DateTime AddTimeForSort => Galgame.AddTime;
    [JsonIgnore][BsonIgnore]
    public string PathForSort => Path ?? string.Empty;

    /// <summary>
    /// 通知界面刷新由逻辑游戏决定的条目状态。
    /// </summary>
    internal void RaiseGameStateChanged() => OnPropertyChanged(nameof(IsPreferred));
}

/// <summary>
/// 库内游戏条目的持久化结构。
/// </summary>
public class GalgameAndPathDbDto
{
    public Guid GalgameId { get; set; } // 关联的逻辑游戏Id
    public Guid EntryId { get; set; } // 库内游戏条目Id
    public string Path { get; set; } = string.Empty; // 游戏在库中的路径
    public LocalInstallationConfig? LocalConfig { get; set; } // 本地安装配置
    
    /// <summary>
    /// 初始化库内游戏条目的持久化结构。
    /// </summary>
    /// <param name="id">逻辑游戏Id</param>
    /// <param name="path">游戏在库中的路径</param>
    /// <param name="entryId">库内游戏条目Id</param>
    /// <param name="localConfig">本地安装配置</param>
    public GalgameAndPathDbDto(Guid id, string path, Guid entryId, LocalInstallationConfig? localConfig)
    {
        GalgameId = id;
        Path = path;
        EntryId = entryId;
        LocalConfig = localConfig;
    }

    /// <summary>
    /// 创建供反序列化使用的空结构。
    /// </summary>
    public GalgameAndPathDbDto() { }
}

/// <summary>
/// 单个可启动安装实例的本机配置。此配置会随游戏库导出，但不会同步到PVN服务器。
/// </summary>
public partial class LocalInstallationConfig : ObservableObject
{
    [ObservableProperty] private string? _exePath; // 启动文件路径
    [ObservableProperty] private string? _exeArguments; // 启动参数
    [ObservableProperty] private string? _processName; // 用于游玩时间记录的进程名
    [ObservableProperty] private string? _textPath; // 要打开的文本路径
    [ObservableProperty] private bool _runAsAdmin; // 是否以管理员权限运行
    [ObservableProperty] private bool _runInLocaleEmulator; // 是否使用转区工具运行
    [ObservableProperty] private bool _highDpi; // 是否启用高DPI替代缩放
    [ObservableProperty] private GamePortablePath? _detectedSavePath; // 当前实例的探测存档路径
    [ObservableProperty] private string? _savePath; // 当前实例的云存档本地路径
    [ObservableProperty] private bool _delayPlayTimeUntilMainWindow; // 声明/启动器窗口切换后再开始计时
    [ObservableProperty] private DateTime _lastSuccessfulLaunchTime = DateTime.MinValue; // 上次成功启动时间

    /// <summary>
    /// 创建当前配置的独立副本。
    /// </summary>
    /// <returns>复制后的安装配置</returns>
    public LocalInstallationConfig Clone() => new()
    {
        ExePath = ExePath,
        ExeArguments = ExeArguments,
        ProcessName = ProcessName,
        TextPath = TextPath,
        RunAsAdmin = RunAsAdmin,
        RunInLocaleEmulator = RunInLocaleEmulator,
        HighDpi = HighDpi,
        DetectedSavePath = DetectedSavePath,
        SavePath = SavePath,
        DelayPlayTimeUntilMainWindow = DelayPlayTimeUntilMainWindow,
        LastSuccessfulLaunchTime = LastSuccessfulLaunchTime,
    };

    /// <summary>
    /// 将配置中位于旧安装目录下的路径迁移到新安装目录。
    /// </summary>
    /// <param name="oldRoot">旧安装根目录</param>
    /// <param name="newRoot">新安装根目录</param>
    /// <returns>迁移路径后的独立配置</returns>
    public LocalInstallationConfig Relocated(string oldRoot, string newRoot)
    {
        LocalInstallationConfig result = Clone();
        result.ExePath = RelocatePath(ExePath, oldRoot, newRoot);
        result.TextPath = RelocatePath(TextPath, oldRoot, newRoot);
        result.SavePath = RelocatePath(SavePath, oldRoot, newRoot);
        if (DetectedSavePath is { } detected)
            result.DetectedSavePath = detected.Relocated(newRoot);
        return result;
    }

    private static string? RelocatePath(string? path, string oldRoot, string newRoot)
    {
        if (string.IsNullOrEmpty(path)) return path;
        string relative = System.IO.Path.GetRelativePath(oldRoot, path);
        return relative == ".." || relative.StartsWith($"..{System.IO.Path.DirectorySeparatorChar}")
            ? path
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(newRoot, relative));
    }
}
