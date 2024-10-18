using System.Collections.ObjectModel;
using System.Globalization;
using Windows.Foundation.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models.Sources;
using Newtonsoft.Json;
using SystemPath = System.IO.Path;

namespace GalgameManager.Models;

public partial class Galgame : ObservableObject, IDisplayableGameObject
{
    public const string DefaultImagePath = "ms-appx:///Assets/WindowIcon.ico";
    public const string DefaultString = "——";
    public const string MetaPath = ".PotatoVN";
    public static readonly int PhraserNumber = 6;
    
    public event GenericDelegate<(Galgame, string)>? GalPropertyChanged;
    public event GenericDelegate<Exception>? ErrorOccurred; //非致命异常产生时触发
    
    public string Path
    {
        get;
        set;
    } = "";
    
    public GalgameSourceType SourceType { get; set; }=GalgameSourceType.UnKnown;
    
    public string Url => $"{SourceType.SourceTypeToString()}://{Path}";

    [JsonIgnore] public GalgameUid Uid => new()
    {
        Name = Name.Value!,
        CnName = CnName,
        BangumiId = Ids[(int)RssType.Bangumi],
        VndbId = Ids[(int)RssType.Vndb],
        PvnId = Ids[(int)RssType.PotatoVn],
    };

    [ObservableProperty] private LockableProperty<string> _imagePath = DefaultImagePath;

    [JsonIgnore] public string? ImageUrl;
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public Dictionary<string, int> PlayedTime = new(); //ShortDateString() -> PlayedTime, 分钟
    [ObservableProperty] private LockableProperty<string> _name = "";
    [ObservableProperty] private string _cnName = "";
    [ObservableProperty] private LockableProperty<string> _description = "";
    [ObservableProperty] private LockableProperty<string> _developer = DefaultString;
    [ObservableProperty] private DateTime _lastPlayTime = DateTime.MinValue; //上次游玩时间（新）
    [ObservableProperty] private LockableProperty<string> _expectedPlayTime = DefaultString;
    [ObservableProperty] private LockableProperty<float> _rating = 0;
    [ObservableProperty] private LockableProperty<DateTime> _releaseDate = DateTime.MinValue;
    [ObservableProperty] private DateTime _lastFetchInfoTime = DateTime.MinValue; //上次搜刮信息时间(i.e.当前信息是什么时候搜刮产生的)
    [ObservableProperty] private ObservableCollection<GalgameCharacter> _characters = new();
    [JsonIgnore][ObservableProperty] private string _savePosition = string.Empty;
    [ObservableProperty] private string? _exePath;
    [ObservableProperty] private LockableProperty<ObservableCollection<string>> _tags;
    [ObservableProperty] private int _totalPlayTime; //单位：分钟
    [ObservableProperty] private bool _runAsAdmin; //是否以管理员权限运行
    private RssType _rssType = RssType.None;
    [ObservableProperty] private PlayType _playType;
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public string?[] Ids = new string?[PhraserNumber]; //magic number: 钦定了一个最大Phraser数目
    [JsonIgnore] public readonly ObservableCollection<Category> Categories = new();
    [JsonIgnore] public ObservableCollection<GalgameSourceBase> Sources { get; } = new(); //所属的源
    [ObservableProperty] private string _comment = string.Empty; //吐槽（评论）
    [ObservableProperty] private int _myRate; //我的评分
    [ObservableProperty] private bool _privateComment; //是否私密评论
    private string? _savePath; //云端存档本地路径
    public string? ProcessName; //手动指定的进程名，用于正确获取游戏进程
    public string? TextPath; //记录的要打开的文本的路径
    public bool PvnUpdate; //是否需要更新
    public PvnUploadProperties PvnUploadProperties; // 要更新到Pvn的属性
    [ObservableProperty] private string _startup_parameters = string.Empty;//启动参数

    
    // 已被废弃的属性，为了兼容旧版本保留（用于反序列化迁移数据）
    [Deprecated($"use {nameof(LastPlayTime)} instead", DeprecationType.Deprecate, 0)]
    [JsonProperty]
    public LockableProperty<string> LastPlay
    {
        set => LastPlayTime = Utils.TryParseDateGuessCulture(value.Value ?? string.Empty);
    }

    [JsonIgnore] public string? Id
    {
        get => Ids[(int)RssType];

        set
        {
            if (Ids[(int)RssType] != value)
            {
               Ids[(int)RssType] = value;
               OnPropertyChanged();
               if (_rssType == RssType.Mixed)
                   UpdateIdFromMixed();
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
                // OnPropertyChanged(); //信息源是通过Combobox选择的，不需要通知
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
        _developer.OnValueChanged += _ => GalPropertyChanged?.Invoke((this, "developer"));
    }

    public Galgame(GalgameSourceType sourceType, string name, string path)
    {
        SourceType = sourceType;
        Name = name;
        Path = path;
        _tags = new ObservableCollection<string>();
        _developer.OnValueChanged += _ => GalPropertyChanged?.Invoke((this, "developer"));
    }

    public Galgame(string name)
    {
        Name = name;
        _tags = new ObservableCollection<string>();
        _developer.OnValueChanged += _ => GalPropertyChanged?.Invoke((this, "developer"));
    }

    public override string ToString() => Name.Value ?? string.Empty;

    /// <summary>
    /// 检查游戏文件夹是否存在
    /// </summary>
    public bool CheckExistLocal()
    {
        GalgameSourceBase? s = Sources.FirstOrDefault(s => s.SourceType == GalgameSourceType.LocalFolder);
        return s != null && Directory.Exists(s.GetPath(this));
    }
    
    public bool CheckIsZip()
    {
        return SourceType == GalgameSourceType.LocalZip;
    }

    /// <summary>
    /// 该游戏是否是本地游戏（存在于某个本地文件夹库中）
    /// </summary>
    public bool IsLocalGame => Sources.Any(s => s.SourceType == GalgameSourceType.LocalFolder);

    /// <summary>
    /// 删除游戏文件夹
    /// </summary>
    public void Delete()
    {
        new DirectoryInfo(Path).Delete(true);
    }
    
    /// <summary>
    /// 时间转换
    /// </summary>
    /// <param name="time">年/月/日</param>
    /// <returns></returns>
    public static long GetTime(string time)
    {
        if (time == DefaultString)
            return 0;
        if (DateTime.TryParseExact(time, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateTime dateTime))
        {
            return (long)(dateTime - DateTime.MinValue).TotalDays;
        }

        return 0;
    }

    /// <summary>
    /// 获取游戏文件夹下的所有exe以及bat文件
    /// </summary>
    /// <returns>所有exe以及bat文件地址</returns>
    public List<string> GetExesAndBats()
    {
        List<string> result = Directory.GetFiles(Path).Where(file => file.ToLower().EndsWith(".exe")).ToList();
        result.AddRange(Directory.GetFiles(Path).Where(file => file.ToLower().EndsWith(".bat")));
        result.AddRange(Directory.GetFiles(Path).Where(file => file.ToLower().EndsWith(".lnk")));
        return result;
    }
    
    /// <summary>
    /// 获取游戏文件夹下的所有子文件夹
    /// </summary>
    /// <returns>子文件夹地址</returns>
    public List<string> GetSubFolders()
    {
        List<string> result = Directory.GetDirectories(Path).ToList();
        return result;
    }

    /// <summary>
    /// 获取该游戏信息文件夹地址
    /// </summary>
    /// <returns></returns>
    public string GetMetaPath()
    {
        return SourceType switch
        {
            GalgameSourceType.LocalFolder => SystemPath.Combine(Path, MetaPath),
            GalgameSourceType.LocalZip when SystemPath.GetDirectoryName(Path) is { } p => 
                SystemPath.Combine(p, MetaPath, SystemPath.GetFileNameWithoutExtension(Path)),
            _ => ""
        };
    }

    /// <summary>
    /// 获取用来保存meta信息的galgame，用于序列化
    /// </summary>
    /// <param name="metaPath">meta文件夹路径</param>
    /// <param name="gamePath">游戏文件夹路径</param>
    /// <returns></returns>
    public Galgame GetMetaCopy(string metaPath, string gamePath)
    {
        Dictionary<string, int> playTime = new();
        foreach (var (key, value) in PlayedTime)
            playTime.Add(key, value);
        ObservableCollection<GalgameCharacter> characters = new();
        foreach (var character in Characters)
        {
            characters.Add(new GalgameCharacter
            {
                Name = character.Name,
                Relation = character.Relation,
                PreviewImagePath = $".{SystemPath.DirectorySeparatorChar}" +
                                   SystemPath.GetFileName(character.PreviewImagePath),
                ImagePath = $".{SystemPath.DirectorySeparatorChar}" + SystemPath.GetFileName(character.ImagePath),
                Summary = character.Summary,
                Gender = character.Gender,
                BirthYear = character.BirthYear,
                BirthMon = character.BirthMon,
                BirthDay = character.BirthDay,
                BirthDate = character.BirthDate,
                BloodType = character.BloodType,
                Height = character.Height,
                Weight = character.Weight,
                BWH = character.BWH,
            });
        }
        Galgame result = new()
        {
            SourceType = SourceType, 
            ImagePath = ImagePath.Value is null or DefaultImagePath ? DefaultImagePath :
                $".{SystemPath.DirectorySeparatorChar}" + SystemPath.GetFileName(ImagePath),
            PlayedTime = playTime,
            Name = Name.Value ?? string.Empty,
            Characters = characters, 
            CnName = CnName,
            Description = Description.Value ?? string.Empty,
            Developer = Developer.Value ?? DefaultString,
            LastPlayTime = LastPlayTime,
            ExpectedPlayTime = ExpectedPlayTime.Value ?? DefaultString,
            Rating = Rating.Value,
            ReleaseDate = ReleaseDate.Value,
            ExePath = ExePath is null ? null : SystemPath.GetRelativePath(metaPath, ExePath),
            Tags = new ObservableCollection<string>(Tags.Value!.ToList()),
            TotalPlayTime = TotalPlayTime,
            RunAsAdmin = RunAsAdmin,
            PlayType = PlayType,
            Ids = Ids,
            RssType = RssType,
            Comment = Comment,
            MyRate = MyRate,
            PrivateComment = PrivateComment,
            SavePath = SavePath,
            ProcessName = ProcessName,
            TextPath =  TextPath,
        };
        return result;
    }

    /// <summary>
    /// 从meta信息中恢复游戏信息
    /// </summary>
    /// <param name="meta">待恢复的数据</param>
    /// <param name="metaFolderPath">meta文件夹路径</param>
    /// <returns>恢复过后的信息</returns>
    public static Galgame ResolveMetaFromLocalFolder(Galgame meta,string metaFolderPath)
    {
        if(meta.SourceType is not (GalgameSourceType.LocalFolder or GalgameSourceType.LocalZip))return meta;
        meta = App.GetService<IFileService>().Read<Galgame>(metaFolderPath, "meta.json")!;
        meta.Path = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, meta.Path));
        if (meta.Path.EndsWith('\\')) meta.Path = meta.Path[..^1];
        if (meta.ImagePath.Value != DefaultImagePath)
        {
            meta.ImagePath.Value = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, meta.ImagePath.Value!));
            if(File.Exists(meta.ImagePath) == false)
                meta.ImagePath.Value = DefaultImagePath;
        }
        foreach (GalgameCharacter character in meta.Characters)
        {
            character.ImagePath = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, character.ImagePath));
            if (!File.Exists(character.ImagePath))
                character.ImagePath = DefaultImagePath;
            character.PreviewImagePath = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, character.PreviewImagePath));
            if (!File.Exists(character.PreviewImagePath))
                character.PreviewImagePath = DefaultImagePath;
        }
        meta.UpdateIdFromMixed();
        if (meta.SourceType == GalgameSourceType.LocalFolder)
        {
            if (meta.ExePath != null)
            {
                meta.ExePath = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, meta.ExePath));
                if (!File.Exists(meta.ExePath))
                    meta.ExePath = null;
            }
            meta.SavePath = Directory.Exists(meta.SavePath) ? meta.SavePath : null; //检查存档路径是否存在并设置SavePosition字段
            meta.FindSaveInPath();
        }
        else
        {
            meta.ExePath = null;
        }
        return meta;
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnPlayTypeChanged(PlayType value)
    {
        GalPropertyChanged?.Invoke((this, "playType"));
    }

    /// <summary>
    /// 从混合数据源的id更新其他数据源的id
    /// </summary>
    public void UpdateIdFromMixed()
    {
        Dictionary<string, string?> tmp = MixedPhraser.Id2IdDict(Ids[(int)RssType.Mixed] ?? "");
        if (tmp.TryGetValue("bgm", out var bgm) && bgm != "null" && bgm != null)
        {
            Ids[(int)RssType.Bangumi] = bgm;
        }
        if (tmp.TryGetValue("vndb", out var vndb) && vndb != "null"&& vndb != null)
        {
            Ids[(int)RssType.Vndb] = vndb;
        }
        if (tmp.TryGetValue("ymgal", out var ymgal) && ymgal != "null"&& ymgal != null)
        {
            Ids[(int)RssType.Ymgal] = ymgal;
        }
    }

    /// <summary>
    /// 试图从游戏根目录中找到存档位置（仅能找到已同步到服务器的存档）
    /// </summary>
    public void FindSaveInPath()
    {
        if (!CheckExistLocal()) return;
        try
        {
            var cnt = 0;
            string? result = null;
            foreach (var subDir in Directory.GetDirectories(Path))
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
    
    public string GetLogName() => $"Galgame_{Url.ToBase64().Replace("/", "").Replace("=", "")}.txt";
    
    public bool ApplySearchKey(string searchKey)
    {
        return Name.Value!.ContainX(searchKey) || 
               Developer.Value!.ContainX(searchKey) || 
               Tags.Value!.Any(str => str.ContainX(searchKey));
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
}
