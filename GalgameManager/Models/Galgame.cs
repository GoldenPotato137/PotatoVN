using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using LiteDB;
using Newtonsoft.Json;

namespace GalgameManager.Models;

public partial class Galgame : ObservableObject, IDisplayableGameObject
{
    public const string DefaultImagePath = "ms-appx:///Assets/WindowIcon.ico";
    public const string DefaultCharacterImagePath = "ms-appx:///Assets/default_character.jpg";
    
    public const string DefaultString = "——";
    public const string MetaPath = ".PotatoVN";
    public static readonly int PhraserNumber = 8;
    
    public event Action<Galgame, string, object>? GalPropertyChanged;
    public event Action<Exception>? ErrorOccurred; //非致命异常产生时触发
    
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
    [ObservableProperty] private DateTime _lastPlayTime = DateTime.MinValue; //上次游玩时间（新）
    [ObservableProperty] private LockableProperty<string> _expectedPlayTime = DefaultString;
    [ObservableProperty] private LockableProperty<float> _rating = 0;
    [ObservableProperty] private LockableProperty<DateTime> _releaseDate = DateTime.MinValue;
    [ObservableProperty] private DateTime _lastFetchInfoTime = DateTime.MinValue; //上次搜刮信息时间(i.e.当前信息是什么时候搜刮产生的)
    [ObservableProperty] private DateTime _addTime = DateTime.MinValue; //游戏添加时间
    [ObservableProperty] private ObservableCollection<GalgameCharacter> _characters = new();
    [JsonIgnore][BsonIgnore][ObservableProperty] private string _savePosition = string.Empty;
    [ObservableProperty] private int _playCount; //游玩次数
    [ObservableProperty] private string? _exePath;
    [ObservableProperty] private string? _exeArguments;
    [ObservableProperty] private LockableProperty<ObservableCollection<string>> _tags;
    [ObservableProperty] private int _totalPlayTime; //单位：分钟
    [ObservableProperty] private bool _runAsAdmin; //是否以管理员权限运行
    [ObservableProperty] private bool _runInLocaleEmulator; //是否转区运行
    [ObservableProperty] private bool _highDpi; //是否高DPI替代缩放
    [ObservableProperty] private bool _enableMagpie; //是否启用Magpie
    [ObservableProperty] private bool _muteInBackground; //是否在后台时静音游戏
    [ObservableProperty] private bool _quickMinimize; //是否启用一键最小化（每游戏）
    private RssType _rssType = RssType.None;
    [ObservableProperty] private PlayType _playType;
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public string?[] Ids { get; set; } = new string?[PhraserNumber]; //magic number: 钦定了一个最大Phraser数目
    [JsonIgnore][BsonIgnore] public readonly ObservableCollection<Category> Categories = new();
    [JsonIgnore][BsonIgnore] public ObservableCollection<GalgameSourceBase> Sources { get; } = new(); //所属的源
    [ObservableProperty] private string _comment = string.Empty; //吐槽（评论）
    [ObservableProperty] private int _myRate; //我的评分
    [ObservableProperty] private bool _privateComment; //是否私密评论
    private string? _savePath; //云端存档本地路径

    public string? ProcessName { get; set; } //手动指定的进程名，用于正确获取游戏进程
    public string? TextPath { get; set; } //记录的要打开的文本的路径
    public bool PvnUpdate { get; set; } //是否需要更新
    public PvnUploadProperties PvnUploadProperties { get; set; } // 要更新到Pvn的属性
    [JsonIgnore] public long PvnLastCharacterFetchTime { get; set; } // 上次从Pvn下载角色信息的时间
    /// 某个游戏的自动获取字段的状态（旧版本不存在的字段在新版本中点进游戏详情也后会试图自动获取）
    public GalgameAutoFetchStatus AutoFetchStatus { get; set; } = new();

    #region OBSOLETE_PROPERTIES //已被废弃的属性，为了兼容旧版本保留（用于反序列化迁移数据）

    [Obsolete($"use {nameof(LastPlayTime)} instead")]
    [JsonProperty]
    public LockableProperty<string> LastPlay
    {
        set => LastPlayTime = Utils.TryParseDateGuessCulture(value.Value ?? string.Empty);
    }
    
    [Obsolete($"Use {nameof(LocalPath)} instead")][BsonIgnore]
    public string Path { get; set; } = "";
    #endregion

    [JsonIgnore][BsonIgnore]
    public string? Id
    {
        get 
        {
            if (Ids.Length < PhraserNumber) Ids = Ids.ResizeArray(PhraserNumber);
            return Ids[(int)RssType];
        }

        set
        {
            if (Ids.Length < PhraserNumber) Ids = Ids.ResizeArray(PhraserNumber);
            if (Ids[(int)RssType] != value)
            {
               Ids[(int)RssType] = value;
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
    
    public string? SavePath
    {
        get => _savePath;
        set
        {
            _savePath = value;
            UiThreadInvokeHelper.Invoke(() =>
            {
                SavePosition = _savePath is null ? "Galgame_SavePath_Local".GetLocalized() : "Galgame_SavePath_Remote".GetLocalized();
            });
        }
    }

    public Galgame()
    {
        _tags = new ObservableCollection<string>();
        _developer.OnValueChanged += _ => GalPropertyChanged?.Invoke(this, nameof(Developer), Developer);
        Sources.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LocalPath));
    }

    public Galgame(string name) : this()
    {
        Name = name;
    }

    public override string ToString() => Name.Value ?? string.Empty;

    /// <summary>
    /// 检查游戏文件夹是否存在
    /// </summary>
    /// <param name="targetType">目标类型，可以为localfolder或steam</param>
    public bool CheckExistLocal(GalgameSourceType targetType = GalgameSourceType.LocalFolder)
    {
        GalgameSourceBase? s = Sources.FirstOrDefault(s => s.SourceType == targetType);
        return s != null && Directory.Exists(s.GetPath(this));
    }

    /// <summary>
    /// 该游戏是否是本地游戏（存在于某个本地文件夹库中）
    /// </summary>
    [JsonIgnore][BsonIgnore]
    public bool IsLocalGame => Sources.Any(s =>
        s.SourceType == GalgameSourceType.LocalFolder || s.SourceType == GalgameSourceType.Steam);

    /// <summary>
    /// 删除游戏文件夹
    /// </summary>
    public void Delete()
    {
        if (LocalPath is not { } path) return;
        new DirectoryInfo(path).Delete(true);
    }

    /// <summary>
    /// 获取该游戏的本地文件夹路径，若其不是本地游戏则返回null
    /// </summary>
    [JsonIgnore]
    [BsonIgnore]
    public string? LocalPath =>
        Sources.FirstOrDefault(s => s.SourceType is GalgameSourceType.LocalFolder or GalgameSourceType.Steam)
            ?.GetPath(this);

    /// <summary>
    /// 获取游戏文件夹下的所有exe以及bat文件
    /// </summary>
    /// <returns>所有exe以及bat文件地址</returns>
    public List<string> GetExesAndBats()
    {
        var path = LocalPath;
        if (path is null) return new List<string>();
        List<string> result = Directory.GetFiles(path).Where(file => file.ToLower().EndsWith(".exe")).ToList();
        result.AddRange(Directory.GetFiles(path).Where(file => file.ToLower().EndsWith(".bat")));
        result.AddRange(Directory.GetFiles(path).Where(file => file.ToLower().EndsWith(".lnk")));
        return result;
    }
    
    /// <summary>
    /// 获取游戏文件夹下的所有子文件夹
    /// </summary>
    /// <returns>子文件夹地址</returns>
    public List<string> GetSubFolders()
    {
        if (LocalPath is null) return [];
        List<string> result = Directory.GetDirectories(LocalPath).ToList();
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

    /// <summary>
    /// 试图从游戏根目录中找到存档位置（仅能找到已同步到服务器的存档）
    /// </summary>
    public void FindSaveInPath()
    {
        if (!CheckExistLocal() || LocalPath is not { } path) return;
        try
        {
            var cnt = 0;
            string? result = null;
            foreach (var subDir in Directory.GetDirectories(path))
                if (FolderOperations.IsSymbolicLink(subDir))
                {
                    cnt++;
                    result = subDir;
                }
            if (cnt == 1)
                SavePath = result;
        }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke(e);
        }
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
    
    public bool ApplySearchKey(string searchKey)
    {
        return Name.Value!.ContainX(searchKey) || 
               ChineseName.Value!.ContainX(searchKey) ||
               OriginalName.Value!.ContainX(searchKey) ||
               Developer.Value!.ContainX(searchKey) || 
               Tags.Value!.Any(str => str.ContainX(searchKey));
    }

    /// 触发属性变更事件，用于手动更新页面
    public void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

    partial void OnLastPlayTimeChanged(DateTime value) => GalPropertyChanged?.Invoke(this, nameof(LastPlayTime), value);
    partial void OnPlayTypeChanged(PlayType value) => GalPropertyChanged?.Invoke(this, nameof(PlayType), value);

    // 监控游戏exe路径变化，如果exe路径变化则设置HighDpi为false
    partial void OnExePathChanged(string? value)
    {
        if (value != null)
            HighDpi = false;
    }

    public class GalgameAutoFetchStatus
    {
        public bool HeaderImage { get; set; }
        public bool Staff { get; set; }
    }
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
    Path
}

public enum DisplayName
{
    ChineseName,
    OriginalName,
    Name,
    None
}
