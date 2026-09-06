using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;
using GalgameManager.Contracts;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using LiteDB;
using Newtonsoft.Json;
using GalgameManager.WinApp.Base.Helpers;

namespace GalgameManager.Models;

public partial class Galgame : ObservableObject, IDisplayableGameObject
{
    public const string DefaultImagePath = "ms-appx:///Assets/WindowIcon.ico";
    public const string DefaultCharacterImagePath = "ms-appx:///Assets/default_character.jpg";

    public const string DefaultString = "——";
    public const string MetaPath = ".PotatoVN";
    public static readonly int PhraserNumber = 9;

    public event Action<Galgame, string, object>? GalPropertyChanged;
    [JsonIgnore] public Action<Exception>? ErrorOccurred; //非致命异常产生时触发

    [JsonIgnore][BsonIgnore]
    public GalgameUid Uid
    {
        get
        {
            if (Ids.Length < PhraserNumber) Ids = Ids.ResizeArray(PhraserNumber);
            return new()
            {
                Name = Name.Value!,
                CnName = CnName,
                BangumiId = Ids[(int)RssType.Bangumi],
                VndbId = Ids[(int)RssType.Vndb],
                YmgalId = Ids[(int)RssType.Ymgal],
                PvnId = Ids[(int)RssType.PotatoVn],
                SteamAppId = Ids[(int)RssType.Steam],
            };
        }
    }

    /// 唯一标识， 若要判断两个游戏是否为同一个游戏，应使用<see cref="GalgameUid"/>
    [BsonId] public Guid Uuid { get; set; }  = Guid.NewGuid();

    [ObservableProperty] private LockableProperty<string> _imagePath = DefaultImagePath;
    [ObservableProperty] private LockableProperty<string?> _headerImagePath = new(null);

    [JsonIgnore][BsonIgnore] public string? ImageUrl;
    [JsonIgnore][BsonIgnore] public List<string> AlternateImageUrls = [];
    public string? HeaderImageUrl { get; set; }
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public Dictionary<string, int> PlayedTime { get; set; }= new(); //ShortDateString() -> PlayedTime, 分钟
    [ObservableProperty] private LockableProperty<string> _name = "";
    [ObservableProperty] private string _cnName = "";
    [ObservableProperty] private LockableProperty<string> _originalName = "";
    [ObservableProperty] private LockableProperty<string> _chineseName = "";
    [ObservableProperty] private LockableProperty<string> _description = "";
    [ObservableProperty] private LockableProperty<string> _developer = DefaultString;
    [ObservableProperty] private LockableProperty<string> _engine = DefaultString;
    [ObservableProperty] private DateTime _lastPlayTime = DateTime.MinValue; //上次游玩时间（新）
    [ObservableProperty] private LockableProperty<string> _expectedPlayTime = DefaultString;
    [ObservableProperty] private LockableProperty<float> _rating = 0;
    [ObservableProperty] private LockableProperty<DateTime> _releaseDate = DateTime.MinValue;
    [ObservableProperty] private DateTime _lastFetchInfoTime = DateTime.MinValue; //上次搜刮信息时间(i.e.当前信息是什么时候搜刮产生的)
    [ObservableProperty] private DateTime _addTime = DateTime.MinValue; //游戏添加时间
    [ObservableProperty] private ObservableCollection<GalgameCharacter> _characters = new();
    [JsonIgnore][BsonIgnore][ObservableProperty] private string _savePosition = string.Empty;
    [ObservableProperty] private int _playCount; //游玩次数
    [ObservableProperty] private LockableProperty<ObservableCollection<string>> _tags;
    [ObservableProperty] private int _totalPlayTime; //单位：分钟
    [ObservableProperty] private bool _enableMagpie; //是否启用Magpie
    [ObservableProperty] private bool _muteInBackground; //是否在后台时静音游戏
    [ObservableProperty] private bool _keyReMap; //是否快捷键映射

    private GamePortablePath? _legacyDetectedSavePath; // 旧版游戏级探测存档路径，仅用于迁移与兼容

    public List<KeyMapping> KeyMappings { get; set; } = new(); //快捷键映射
    private RssType _rssType = RssType.None;
    [ObservableProperty] private PlayType _playType;
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public string?[] Ids { get; set; } = new string?[PhraserNumber]; //magic number: 钦定了一个最大Phraser数目
    /// 插件专用的id存储
    public Dictionary<int,  string?> IdForPlugins { get; set; } = [];
    [JsonIgnore][BsonIgnore] public readonly ObservableCollection<Category> Categories = new();
    [JsonIgnore][BsonIgnore] public ObservableCollection<GalgameSourceBase> Sources { get; } = new(); //所属的源
    private readonly ObservableCollection<GalgameAndPath> _sourceEntries = []; // 游戏所属库条目的内部可变集合
    /// <summary>
    /// 游戏所属库条目的只读视图。条目的增删必须通过游戏库集合服务完成。
    /// </summary>
    [JsonIgnore][BsonIgnore] public ReadOnlyObservableCollection<GalgameAndPath> SourceEntries { get; }
    [ObservableProperty] private Guid? _preferredInstallationId; // 当前首选（上次成功启动）的安装实例Id
    [ObservableProperty] private string _comment = string.Empty; //吐槽（评论）
    [ObservableProperty] private int _myRate; //我的评分
    [ObservableProperty] private bool _privateComment; //是否私密评论
    private string? _legacyExePath; // 旧版游戏级启动文件路径
    private string? _legacyExeArguments; // 旧版游戏级启动参数
    private string? _legacyProcessName; // 旧版游戏级进程名
    private string? _legacyTextPath; // 旧版游戏级文本路径
    private string? _legacySavePath; // 旧版游戏级云存档本地路径
    private bool _legacyRunAsAdmin; // 旧版游戏级管理员运行设置
    private bool _legacyRunInLocaleEmulator; // 旧版游戏级转区运行设置
    private bool _legacyHighDpi; // 旧版游戏级高DPI设置

    public bool PvnUpdate { get; set; } //是否需要更新
    public PvnUploadProperties PvnUploadProperties { get; set; } // 要更新到Pvn的属性
    [JsonIgnore] public long PvnLastCharacterFetchTime { get; set; } // 上次从Pvn下载角色信息的时间
    /// 某个游戏的自动获取字段的状态（旧版本不存在的字段在新版本中点进游戏详情也后会试图自动获取）
    public GalgameAutoFetchStatus AutoFetchStatus { get; set; } = new();

    #region OBSOLETE_PROPERTIES //已被废弃的属性，为了兼容旧版本保留（用于反序列化迁移数据 / 兼容旧插件等）

    [Obsolete($"use {nameof(LastPlayTime)} instead")]
    [JsonProperty]
    public LockableProperty<string> LastPlay
    {
        set => LastPlayTime = Utils.TryParseDateGuessCulture(value.Value ?? string.Empty);
    }

    [Obsolete($"Use {nameof(LocalPath)} instead")][BsonIgnore]
    public string Path { get; set; } = "";

    [Obsolete("Use DetectedSavePath instead")][BsonIgnore]
    public string? DetectedSavePosition { get; set; }

    #endregion

    [JsonIgnore][BsonIgnore]
    public string? Id
    {
        get
        {
            if ((int)RssType >= 100) return IdForPlugins.GetValueOrDefault((int)RssType); //插件用的id
            if (Ids.Length < PhraserNumber) Ids = Ids.ResizeArray(PhraserNumber);
            return Ids[(int)RssType];
        }

        set
        {
            if (Ids.Length < PhraserNumber && (int)RssType < 100) Ids = Ids.ResizeArray(PhraserNumber);
            if (((int)RssType < 100 && Ids[(int)RssType] != value) || (int)RssType >= 100)
            {
                if ((int) RssType < 100) Ids[(int)RssType] = value;
                else IdForPlugins[(int)RssType] = value;
                OnPropertyChanged();
                if (_rssType == RssType.Mixed) UpdateIdFromMixed();
                else UpdateMixedId();
            }
        }
    }

    public RssType RssType
    {
        get => _rssType;
        set
        {
            if (_rssType != value)
            {
                _rssType = value;
                OnPropertyChanged(); //通常情况下信息源是通过Combobox选择的，但更新游戏信息时需要手动触发
                OnPropertyChanged(nameof(Id));
            }
        }
    }

    /// <summary>
    /// 当前首选的可启动安装实例。文件操作应显式传入安装实例，不应依赖此兼容视图。
    /// </summary>
    [JsonIgnore][BsonIgnore]
    public GalgameAndPath? PreferredLocalInstallation =>
        SourceEntries.FirstOrDefault(e => e.EntryId == PreferredInstallationId && e.IsLocalInstallation)
        ?? SourceEntries.Where(e => e.IsLocalInstallation)
            .OrderByDescending(e => e.LocalConfig?.LastSuccessfulLaunchTime ?? DateTime.MinValue)
            .FirstOrDefault();

    /// <summary>
    /// 当前游戏所有本地安装实例的只读快照。
    /// </summary>
    [JsonIgnore][BsonIgnore]
    public IReadOnlyList<GalgameAndPath> LocalInstallations =>
        SourceEntries.Where(e => e.IsLocalInstallation).ToList();

    #region OBSOLETE_PROPERTIES // 已废弃的游戏级安装属性，仅用于旧数据反序列化与旧插件兼容

    // 新代码应使用明确安装实例上的LocalInstallationConfig。
    [Obsolete("Use PreferredLocalInstallation.LocalConfig.ExePath or an explicit installation instead.")]
    public string? ExePath
    {
        get => PreferredLocalInstallation?.LocalConfig?.ExePath ?? _legacyExePath;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.ExePath = value;
            if (SetProperty(ref _legacyExePath, value)) OnPropertyChanged();
            if (value is not null) HighDpi = false;
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.ExeArguments or an explicit installation instead.")]
    public string? ExeArguments
    {
        get => PreferredLocalInstallation?.LocalConfig?.ExeArguments ?? _legacyExeArguments;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.ExeArguments = value;
            SetProperty(ref _legacyExeArguments, value);
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.ProcessName or an explicit installation instead.")]
    public string? ProcessName
    {
        get => PreferredLocalInstallation?.LocalConfig?.ProcessName ?? _legacyProcessName;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.ProcessName = value;
            SetProperty(ref _legacyProcessName, value);
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.TextPath or an explicit installation instead.")]
    public string? TextPath
    {
        get => PreferredLocalInstallation?.LocalConfig?.TextPath ?? _legacyTextPath;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.TextPath = value;
            SetProperty(ref _legacyTextPath, value);
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.RunAsAdmin or an explicit installation instead.")]
    public bool RunAsAdmin
    {
        get => PreferredLocalInstallation?.LocalConfig?.RunAsAdmin ?? _legacyRunAsAdmin;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.RunAsAdmin = value;
            SetProperty(ref _legacyRunAsAdmin, value);
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.RunInLocaleEmulator or an explicit installation instead.")]
    public bool RunInLocaleEmulator
    {
        get => PreferredLocalInstallation?.LocalConfig?.RunInLocaleEmulator ?? _legacyRunInLocaleEmulator;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.RunInLocaleEmulator = value;
            SetProperty(ref _legacyRunInLocaleEmulator, value);
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.HighDpi or an explicit installation instead.")]
    public bool HighDpi
    {
        get => PreferredLocalInstallation?.LocalConfig?.HighDpi ?? _legacyHighDpi;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.HighDpi = value;
            SetProperty(ref _legacyHighDpi, value);
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.DetectedSavePath or an explicit installation instead.")]
    public GamePortablePath? DetectedSavePath
    {
        get => PreferredLocalInstallation?.LocalConfig?.DetectedSavePath ?? _legacyDetectedSavePath;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.DetectedSavePath = value;
            SetProperty(ref _legacyDetectedSavePath, value);
        }
    }

    [Obsolete("Use PreferredLocalInstallation.LocalConfig.SavePath or an explicit installation instead.")]
    public string? SavePath
    {
        get => PreferredLocalInstallation?.LocalConfig?.SavePath ?? _legacySavePath;
        set
        {
            LocalInstallationConfig? config = PreferredLocalInstallation?.LocalConfig;
            if (config is not null) config.SavePath = value;
            SetProperty(ref _legacySavePath, value);
            SavePosition = (value is null
                ? "Galgame_SavePath_Local".GetLocalized()
                : "Galgame_SavePath_Remote".GetLocalized()) ?? string.Empty;
        }
    }

    #endregion

    /// <summary>
    /// 初始化一个空的逻辑游戏。
    /// </summary>
    public Galgame()
    {
        _tags = new ObservableCollection<string>();
        SourceEntries = new ReadOnlyObservableCollection<GalgameAndPath>(_sourceEntries);
        Sources.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LocalPath));
        _sourceEntries.CollectionChanged += (_, _) => RaiseInstallationPropertiesChanged();
        // 绑定初始LockableProperty实例，否则未经整体替换的对象修改.Value不会触发GalPropertyChanged
        // （整体替换属性时由OnDeveloperChanging/OnEngineChanging解绑旧实例并绑定新实例）
        _developer.OnValueChanged += HandleDeveloperValueChanged;
        _engine.OnValueChanged += HandleEngineValueChanged;
    }

    /// <summary>
    /// 使用游戏名初始化逻辑游戏。
    /// </summary>
    /// <param name="name">游戏名</param>
    public Galgame(string name) : this()
    {
        Name = name;
    }

    public override string ToString() => Name.Value ?? string.Empty;

    /// <summary>
    /// 将库内游戏条目挂接到当前逻辑游戏。仅供游戏库集合服务在建立关系时调用。
    /// </summary>
    /// <param name="entry">要挂接的库内游戏条目</param>
    public void AttachSourceEntry(GalgameAndPath entry)
    {
        if (_sourceEntries.All(e => e.EntryId != entry.EntryId))
            _sourceEntries.Add(entry);
        if (entry.Source is not null && !Sources.Contains(entry.Source))
            Sources.Add(entry.Source);

        if (!entry.IsLocalInstallation) return;
        entry.LocalConfig ??= new LocalInstallationConfig();
        if (PreferredInstallationId is null)
        {
            ApplyLegacyLocalConfiguration(entry, overwrite: false);
            PreferredInstallationId = entry.EntryId;
        }
        RaiseInstallationPropertiesChanged();
    }

    /// <summary>
    /// 从当前逻辑游戏解除库内游戏条目。仅供游戏库集合服务在移除关系时调用。
    /// </summary>
    /// <param name="entry">要解除的库内游戏条目</param>
    public void DetachSourceEntry(GalgameAndPath entry)
    {
        _sourceEntries.Remove(entry);
        if (entry.Source is not null && SourceEntries.All(e => e.Source != entry.Source))
            Sources.Remove(entry.Source);
        if (PreferredInstallationId == entry.EntryId)
        {
            PreferredInstallationId = SourceEntries.Where(e => e.IsLocalInstallation)
                .OrderByDescending(e => e.LocalConfig?.LastSuccessfulLaunchTime ?? DateTime.MinValue)
                .Select(e => (Guid?)e.EntryId)
                .FirstOrDefault();
        }
        RaiseInstallationPropertiesChanged();
    }

    /// <summary>
    /// 将指定本地安装实例设为首选实例。
    /// </summary>
    /// <param name="installation">属于当前游戏的本地安装实例</param>
    public void SetPreferredInstallation(GalgameAndPath installation)
    {
        if (!installation.IsLocalInstallation || !SourceEntries.Contains(installation))
            throw new ArgumentException("The installation does not belong to this game.", nameof(installation));
        PreferredInstallationId = installation.EntryId;
        RaiseInstallationPropertiesChanged();
    }

    /// <summary>
    /// 校验首选安装实例；首选实例无效时按最近成功启动时间选择回退实例。
    /// </summary>
    public void EnsurePreferredInstallation()
    {
        if (SourceEntries.Any(e => e.IsLocalInstallation && e.EntryId == PreferredInstallationId))
            return;
        GalgameAndPath? fallback = SourceEntries.Where(e => e.IsLocalInstallation)
            .OrderByDescending(e => e.LocalConfig?.LastSuccessfulLaunchTime ?? DateTime.MinValue)
            .FirstOrDefault();
        PreferredInstallationId = fallback?.EntryId;
        if (fallback is not null) ApplyLegacyLocalConfiguration(fallback, overwrite: false);
        RaiseInstallationPropertiesChanged();
    }

    /// <summary>
    /// 从旧版游戏级字段创建安装实例配置。
    /// </summary>
    /// <param name="installationPath">安装实例根目录</param>
    /// <returns>转换后的安装实例配置</returns>
    public LocalInstallationConfig CreateLegacyLocalConfiguration(string installationPath)
    {
        LocalInstallationConfig result = new();
        ApplyLegacyLocalConfiguration(result, installationPath, overwrite: true);
        return result;
    }

    /// <summary>
    /// 将安装实例配置载入旧版游戏级兼容字段。
    /// </summary>
    /// <param name="config">要载入的安装实例配置</param>
    public void LoadLegacyLocalConfiguration(LocalInstallationConfig config)
    {
        _legacyExePath = config.ExePath;
        _legacyExeArguments = config.ExeArguments;
        _legacyProcessName = config.ProcessName;
        _legacyTextPath = config.TextPath;
        _legacyRunAsAdmin = config.RunAsAdmin;
        _legacyRunInLocaleEmulator = config.RunInLocaleEmulator;
        _legacyHighDpi = config.HighDpi;
        _legacyDetectedSavePath = config.DetectedSavePath;
        _legacySavePath = config.SavePath;
        RaiseInstallationPropertiesChanged();
    }

    /// <summary>
    /// 将旧版游戏级字段迁移到指定安装实例。
    /// </summary>
    /// <param name="installation">迁移目标安装实例</param>
    /// <param name="overwrite">是否覆盖目标实例已有配置</param>
    public void ApplyLegacyLocalConfiguration(GalgameAndPath installation, bool overwrite)
    {
        installation.LocalConfig ??= new LocalInstallationConfig();
        ApplyLegacyLocalConfiguration(installation.LocalConfig, installation.Path, overwrite);
    }

    private void ApplyLegacyLocalConfiguration(LocalInstallationConfig config, string installationPath, bool overwrite)
    {
        if (overwrite || string.IsNullOrEmpty(config.ExePath)) config.ExePath = _legacyExePath;
        if (overwrite || string.IsNullOrEmpty(config.ExeArguments)) config.ExeArguments = _legacyExeArguments;
        if (overwrite || string.IsNullOrEmpty(config.ProcessName)) config.ProcessName = _legacyProcessName;
        if (overwrite || string.IsNullOrEmpty(config.TextPath)) config.TextPath = _legacyTextPath;
        if (overwrite || config.SavePath is null) config.SavePath = _legacySavePath;
        if (overwrite || config.DetectedSavePath is null)
            config.DetectedSavePath = _legacyDetectedSavePath?.Relocated(installationPath);
        if (overwrite || !config.RunAsAdmin) config.RunAsAdmin = _legacyRunAsAdmin;
        if (overwrite || !config.RunInLocaleEmulator) config.RunInLocaleEmulator = _legacyRunInLocaleEmulator;
        if (overwrite || !config.HighDpi) config.HighDpi = _legacyHighDpi;
    }

    private void RaiseInstallationPropertiesChanged()
    {
        foreach (GalgameAndPath entry in SourceEntries)
            entry.RaiseGameStateChanged();
        OnPropertyChanged(nameof(SourceEntries));
        OnPropertyChanged(nameof(LocalInstallations));
        OnPropertyChanged(nameof(PreferredLocalInstallation));
        OnPropertyChanged(nameof(LocalPath));
        OnPropertyChanged(nameof(IsLocalGame));
#pragma warning disable CS0618
        OnPropertyChanged(nameof(ExePath));
        OnPropertyChanged(nameof(ExeArguments));
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(TextPath));
        OnPropertyChanged(nameof(RunAsAdmin));
        OnPropertyChanged(nameof(RunInLocaleEmulator));
        OnPropertyChanged(nameof(HighDpi));
        OnPropertyChanged(nameof(DetectedSavePath));
        OnPropertyChanged(nameof(SavePath));
#pragma warning restore CS0618
    }

    partial void OnPreferredInstallationIdChanged(Guid? value) => RaiseInstallationPropertiesChanged();

    /// <summary>
    /// 检查游戏文件夹是否存在
    /// </summary>
    /// <param name="targetType">目标类型，可以为localfolder或steam</param>
    public bool CheckExistLocal(GalgameSourceType targetType = GalgameSourceType.LocalFolder)
    {
        return SourceEntries.Any(e =>
            e.Source?.SourceType == targetType && e.IsLocalInstallation && Directory.Exists(e.Path));
    }

    /// <summary>
    /// 该游戏是否至少存在于一个实现<see cref="ILocalGalgameSource"/>的本地库中。
    /// </summary>
    [JsonIgnore][BsonIgnore]
    public bool IsLocalGame => SourceEntries.Any(e => e.IsLocalInstallation);

    /// <summary>
    /// 获取该游戏的本地文件夹路径，若其不是本地游戏则返回null
    /// </summary>
    [JsonIgnore]
    [BsonIgnore]
    public string? LocalPath => PreferredLocalInstallation?.Path;

    /// <summary>
    /// 获取游戏文件夹下的所有exe以及bat文件
    /// </summary>
    /// <param name="installation">目标安装实例；为null时使用首选安装实例</param>
    /// <returns>所有exe以及bat文件地址</returns>
    public List<string> GetExesAndBats(GalgameAndPath? installation = null)
    {
        var path = installation?.Path ?? LocalPath;
        if (path is null) return new List<string>();
        List<string> result = Directory.GetFiles(path).Where(file => file.ToLower().EndsWith(".exe")).ToList();
        result.AddRange(Directory.GetFiles(path).Where(file => file.ToLower().EndsWith(".bat")));
        result.AddRange(Directory.GetFiles(path).Where(file => file.ToLower().EndsWith(".lnk")));
        return result;
    }

    /// <summary>
    /// 获取游戏文件夹下的所有子文件夹
    /// </summary>
    /// <param name="installation">目标安装实例；为null时使用首选安装实例</param>
    /// <returns>子文件夹地址</returns>
    public List<string> GetSubFolders(GalgameAndPath? installation = null)
    {
        string? path = installation?.Path ?? LocalPath;
        if (path is null) return [];
        List<string> result = Directory.GetDirectories(path).ToList();
        return result;
    }

    /// <summary>
    /// 获取游戏文件夹根目录下的所有文件
    /// </summary>
    /// <param name="installation">目标安装实例；为null时使用首选安装实例</param>
    /// <returns>子文件夹地址</returns>
    public List<string> GetRootFiles(GalgameAndPath? installation = null)
    {
        string? path = installation?.Path ?? LocalPath;
        if (path is null) return [];
        HashSet<string> commonExtensions = new (StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".xp3", ".lnk", ".txt", ".ico", ".sig", ".dll",
        };
        List<string> result = Directory.GetFiles(path)
            .Where(el => !commonExtensions.Contains(System.IO.Path.GetExtension(el)))
            .ToList();
        return result;
    }

    /// <summary>
    /// 尝试获取游戏的id，以int形式返回 <br/>
    /// 如果id前面有前缀（如v123），会去掉前缀（返回123）<br/>
    /// 如果解析失败或者id为null，则返回-1
    /// </summary>
    /// <param name="type">不能是mixed</param>
    /// <returns></returns>
    public int GetId(RssType type)
    {
        if (type is RssType.Mixed) return -1;
        try
        {
            var id = Ids[(int)type];
            if (string.IsNullOrEmpty(id)) return -1;
            var numStartIndex = 0;
            for(var i = 0; i < id.Length; i++)
            {
                if (!char.IsDigit(id[i])) continue;
                numStartIndex = i;
                break;
            }
            if (int.TryParse(id[numStartIndex..], out var result))
                return result;
            return -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>
    /// 从混合数据源的id更新其他数据源的id
    /// </summary>
    public void UpdateIdFromMixed()
    {
        if (Ids.Length < PhraserNumber) Ids = Ids.ResizeArray(PhraserNumber);
        foreach (RssType rss in RssTypeHelper.UsablePhrasers)
            Ids[(int)rss] = null;
        var ids = Ids[(int)RssType.Mixed] ?? string.Empty.Replace("，", ",").Replace(" ", "");
        foreach (var id in ids.Split(",").Where(s => s.Contains(':')))
        {
            var parts = id.Split(":");
            if (parts.Length != 2) continue;
            if (parts[0].GetRssType() is not { } type) continue;
            Ids[(int)type] = parts[1] == "null" ? null : parts[1];
        }
    }

    /// <summary>
    /// 从其他数据源的id更新混合数据源的id
    /// </summary>
    public void UpdateMixedId()
    {
        if (Ids.Length < PhraserNumber) Ids = Ids.ResizeArray(PhraserNumber);
        // 更新id
        var mixedId = string.Empty;
        foreach (RssType rss in RssTypeHelper.UsablePhrasers)
        {
            var id = Ids[(int)rss];
            mixedId += $"{rss.GetAbbr()}:{id ?? "null"},";
            Ids[(int)rss] = id == "null" ? null : id;
        }
        Ids[(int)RssType.Mixed] = mixedId.TrimEnd(',');
    }

    /// 检查是否所有的id都为空
    public bool IsIdsEmpty() => Ids.All(string.IsNullOrEmpty);

    /// <summary>
    /// 合并各种时间信息<br/>
    /// PlayedTime, LastPlayTime, ReleaseDate
    /// </summary>
    public void MergeTime(Galgame? other)
    {
        if (other is null) return;
        // 合并PlayedTime
        foreach (var (key, value) in other.PlayedTime)
        {
            if (!PlayedTime.TryAdd(key, value))
                PlayedTime[key] = int.Max(value, PlayedTime[key]);
        }
        // 排序PlayedTime
        PlayedTime = PlayedTime.OrderBy(pair => Utils.TryParseDateGuessCulture(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        TotalPlayTime = PlayedTime.Values.Sum();
        LastPlayTime = PlayedTime.Count > 0
            ? PlayedTime.Keys.Select(Utils.TryParseDateGuessCulture).Max()
            : DateTime.MinValue;
        ReleaseDate.Value = other.ReleaseDate.Value > ReleaseDate.Value ? other.ReleaseDate.Value : ReleaseDate.Value;
    }

    public string GetLogName() => $"Galgame_{(Name.Value ?? string.Empty).RemoveInvalidChars()}.txt";

    /// 触发属性变更事件，用于手动更新页面
    public void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

    partial void OnLastPlayTimeChanged(DateTime value) => GalPropertyChanged?.Invoke(this, nameof(LastPlayTime), value);
    partial void OnPlayTypeChanged(PlayType value) => GalPropertyChanged?.Invoke(this, nameof(PlayType), value);

    public class GalgameAutoFetchStatus
    {
        public bool HeaderImage { get; set; }
        public bool Staff { get; set; }
    }

    #region HOOKS

    partial void OnDeveloperChanging(LockableProperty<string>? oldValue, LockableProperty<string> newValue) =>
        RebindLockableProperty(oldValue, newValue, HandleDeveloperValueChanged);

    partial void OnEngineChanging(LockableProperty<string>? oldValue, LockableProperty<string> newValue) =>
        RebindLockableProperty(oldValue, newValue, HandleEngineValueChanged);

    // 提取为具名方法（而非lambda）以保证事件解绑按委托相等性命中：
    // 构造函数绑定初始实例与RebindLockableProperty解绑旧实例必须使用同一方法
    private void HandleDeveloperValueChanged(string? _) =>
        GalPropertyChanged?.Invoke(this, nameof(Developer), Developer);

    private void HandleEngineValueChanged(string? _) =>
        GalPropertyChanged?.Invoke(this, nameof(Engine), Engine);

    private static void RebindLockableProperty<T>(LockableProperty<T>? oldValue, LockableProperty<T> newValue, Action<T?> handler)
    {
        if (oldValue is not null) oldValue.OnValueChanged -= handler;
        newValue.OnValueChanged += handler;
    }

    #endregion
}


public enum SortKeys
{
    Name,
    LastPlay,
    Developer,
    Rating,
    ReleaseDate,
    LastFetchInfoTime,
    AddTime,
    Path,
    Custom,
}

public enum DisplayName
{
    ChineseName,
    OriginalName,
    Name,
    None
}
