using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using Newtonsoft.Json;
using SystemPath = System.IO.Path;

namespace GalgameManager.Models;

public partial class Galgame : ObservableObject, IComparable<Galgame>, ICloneable
{
    public const string DefaultImagePath = "ms-appx:///Assets/WindowIcon.ico";
    public const string DefaultString = "——";
    public const string MetaPath = ".PotatoVN";
    
    public event GenericDelegate<(Galgame, string)>? GalPropertyChanged;
    public event GenericDelegate<Exception>? ErrorOccurred; //非致命异常产生时触发
    
    public string Path
    {
        get;
        set;
    } = "";
    
    [ObservableProperty] private LockableProperty<string> _imagePath = DefaultImagePath;

    [JsonIgnore] public string? ImageUrl;
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public Dictionary<string, int> PlayedTime = new(); //ShortDateString() -> PlayedTime, 分钟
    [ObservableProperty] private LockableProperty<string> _name = "";
    [ObservableProperty] private string _cnName = "";
    [ObservableProperty] private LockableProperty<string> _description = "";
    [ObservableProperty] private LockableProperty<string> _developer = DefaultString;
    [ObservableProperty] private LockableProperty<string> _lastPlay = DefaultString;
    [ObservableProperty] private LockableProperty<string> _expectedPlayTime = DefaultString;
    [ObservableProperty] private LockableProperty<float> _rating = 0;
    [ObservableProperty] private LockableProperty<DateTime> _releaseDate;
    [ObservableProperty] private ObservableCollection<GalgameCharacter> _characters = new();
    [JsonIgnore][ObservableProperty] private string _savePosition = string.Empty;
    [ObservableProperty] private string? _exePath;
    [ObservableProperty] private LockableProperty<ObservableCollection<string>> _tags = new();
    [ObservableProperty] private int _totalPlayTime; //单位：分钟
    [ObservableProperty] private bool _runAsAdmin; //是否以管理员权限运行
    private RssType _rssType = RssType.None;
    [ObservableProperty] private PlayType _playType;
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public string?[] Ids = new string?[5]; //magic number: 钦定了一个最大Phraser数目
    [JsonIgnore] public readonly ObservableCollection<Category> Categories = new();
    [ObservableProperty] private string _comment = string.Empty; //吐槽（评论）
    [ObservableProperty] private int _myRate; //我的评分
    [ObservableProperty] private bool _privateComment; //是否私密评论
    private string? _savePath; //云端存档本地路径
    public string? ProcessName; //手动指定的进程名，用于正确获取游戏进程
    public string? TextPath; //记录的要打开的文本的路径
    public bool PvnUpdate; //是否需要更新
    public PvnUploadProperties PvnUploadProperties; // 要更新到Pvn的属性

    [JsonIgnore] public static bool RecordOnlyWhenForeground; //是否只在游戏处于前台时记录游玩时间
    [JsonIgnore] public static SortKeys[] SortKeysList
    {
        get;
        private set;
    } = { SortKeys.LastPlay , SortKeys.Developer};

    [JsonIgnore] public static bool[] SortKeysAscending
    {
        get;
        private set;
    } = {false, false};

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
        _tags.Value = new ObservableCollection<string>();
        _developer.OnValueChanged += _ => GalPropertyChanged?.Invoke((this, "developer"));
        _releaseDate = DateTime.MinValue;
    }

    public Galgame(string path)
    {
        Name = SystemPath.GetFileName(SystemPath.GetDirectoryName(path + SystemPath.DirectorySeparatorChar)) ?? "";
        _tags.Value = new ObservableCollection<string>();
        _releaseDate = DateTime.MinValue;
        Path = path;
        _developer.OnValueChanged += _ => GalPropertyChanged?.Invoke((this, "developer"));
    }
    
    /// <summary>
    /// 检查游戏文件夹是否存在
    /// </summary>
    public bool CheckExist()
    {
        return Directory.Exists(Path);
    }

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
    /// 更新CompareTo参数，可用于Sort
    /// sortKeysList 和 sortKeysAscending长度相同
    /// </summary>
    /// <param name="sortKeysList"></param>
    /// <param name="sortKeysAscending">升序/降序: true/false</param>
    public static void UpdateSortKeys(SortKeys[] sortKeysList, bool[] sortKeysAscending)
    {
        SortKeysList = sortKeysList;
        SortKeysAscending = sortKeysAscending;
    }
    
    public static void UpdateSortKeys(SortKeys[] sortKeysList)
    {
        SortKeysList = sortKeysList;
    }
    
    public static void UpdateSortKeysAscending(bool[] sortKeysAscending)
    {
        SortKeysAscending = sortKeysAscending;
    }

    public int CompareTo(Galgame? b)
    {
        if (b is null ) return 1;
        for (var i = 0; i < Math.Min(SortKeysList.Length, SortKeysAscending.Length); i++)
        {
            var result = 0;
            var take = SortKeysAscending[i]?-1:1; //true升序, false降序
            switch (SortKeysList[i])
            {
                case SortKeys.Developer:
                    result = string.Compare(Developer.Value!, b.Developer.Value, StringComparison.Ordinal);
                    break;
                case SortKeys.Name:
                    result = string.Compare(Name.Value!, b.Name.Value, StringComparison.CurrentCultureIgnoreCase);
                    take *= -1;
                    break;
                case SortKeys.Rating:
                    result = Rating.Value.CompareTo(b.Rating.Value);
                    break;
                case SortKeys.LastPlay:
                    result = GetTime(LastPlay.Value!).CompareTo(GetTime(b.LastPlay.Value!));
                    break;
                case SortKeys.ReleaseDate:
                    if (ReleaseDate != null && b.ReleaseDate != null )
                    {
                        result = ReleaseDate.Value.CompareTo(b.ReleaseDate.Value);
                    }
                    break;
            }
            if (result != 0)
                return take * result; 
        }
        return 0;
    }
    
    public object Clone()
    {
        Dictionary<string, int> playTime = new();
        foreach (var (key, value) in PlayedTime)
            playTime.Add(key, value);
        Galgame result = new()
        {
            Path = "..\\",
            ImagePath = ImagePath.Value is null or DefaultImagePath ? DefaultImagePath :
                ".\\" + SystemPath.GetFileName(ImagePath),
            PlayedTime = playTime,
            Name = Name.Value ?? string.Empty,
            CnName = CnName,
            Description = Description.Value ?? string.Empty,
            Developer = Developer.Value ?? DefaultString,
            LastPlay = LastPlay.Value ?? DefaultString,
            ExpectedPlayTime = ExpectedPlayTime.Value ?? DefaultString,
            Rating = Rating.Value,
            ReleaseDate = ReleaseDate.Value,
            ExePath = "..\\" + SystemPath.GetFileName(ExePath),
            Tags = new ObservableCollection<string>(Tags.Value!.ToList()),
            TotalPlayTime = TotalPlayTime,
            RunAsAdmin = RunAsAdmin,
            PlayType = PlayType,
            Ids = (string[])Ids.Clone(),
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
    /// 记录游戏的游玩时间
    /// </summary>
    /// <param name="process">游戏进程</param>
    public async void RecordPlayTime(Process process)
    {
        await Task.Run(() =>
        {
            while (!process.HasExited)
            {
                Thread.Sleep(1000 * 60);
                if (process.HasExited || 
                    (RecordOnlyWhenForeground && (process.IsMainWindowMinimized() || !process.IsMainWindowActive())))
                    continue;
                UiThreadInvokeHelper.Invoke(() =>
                {
                    TotalPlayTime++;
                });
                var now = DateTime.Now.ToString("yyyy/M/d");
                if (PlayedTime.ContainsKey(now))
                    PlayedTime[now]++;
                else
                    PlayedTime.Add(now, 1);
            }
        });
    }

    /// <summary>
    /// 获取该游戏信息文件夹地址
    /// </summary>
    /// <returns></returns>
    public string GetMetaPath()
    {
        return SystemPath.Combine(Path, MetaPath);
    }

    /// <summary>
    /// 获取用来保存meta信息的galgame，用于序列化
    /// </summary>
    /// <returns></returns>
    public Galgame GetMetaCopy()
    {
        return (Galgame) Clone();
    }

    /// <summary>
    /// 从meta信息中恢复游戏信息
    /// </summary>
    /// <param name="meta">待恢复的数据</param>
    /// <param name="metaFolderPath">meta文件夹路径</param>
    /// <returns>恢复过后的信息</returns>
    public static Galgame ResolveMeta(Galgame meta,string metaFolderPath)
    {
        meta.Path = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, meta.Path));
        if (meta.Path.EndsWith('\\')) meta.Path = meta.Path[..^1];
        if (meta.ImagePath.Value != DefaultImagePath)
        {
            meta.ImagePath.Value = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, meta.ImagePath.Value!));
            if(File.Exists(meta.ImagePath) == false)
                meta.ImagePath.Value = DefaultImagePath;
        }
        if (meta.ExePath != null)
        {
            meta.ExePath = SystemPath.GetFullPath(SystemPath.Combine(metaFolderPath, meta.ExePath));
            if (File.Exists(meta.ExePath) == false)
                meta.ExePath = null;
        }
        meta.SavePath = Directory.Exists(meta.SavePath) ? meta.SavePath : null; //检查存档路径是否存在并设置SavePosition字段
        meta.UpdateIdFromMixed();
        meta.FindSaveInPath();
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
        (string? bgmId, string? vndbId) tmp = MixedPhraser.TryGetId(Ids[(int)RssType.Mixed]);
        if (tmp.bgmId != null) 
            Ids[(int)RssType.Bangumi] = tmp.bgmId;
        if (tmp.vndbId != null) 
            Ids[(int)RssType.Vndb] = tmp.vndbId;
    }

    /// <summary>
    /// 试图从游戏根目录中找到存档位置（仅能找到已同步到服务器的存档）
    /// </summary>
    public void FindSaveInPath()
    {
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
    
    public string GetLogName() => $"Galgame_{Path.ToBase64().Replace("/", "").Replace("=", "")}.txt";
}


public enum SortKeys
{
    Name,
    LastPlay,
    Developer,
    Rating,
    ReleaseDate
}
