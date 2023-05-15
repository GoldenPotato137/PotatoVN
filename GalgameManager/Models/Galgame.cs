using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Services;
using Newtonsoft.Json;

namespace GalgameManager.Models;

public partial class Galgame : ObservableObject, IComparable<Galgame>
{
    public const string DefaultImagePath = "ms-appx:///Assets/WindowIcon.ico";
    public const string DefaultString = "——";
    public string Path
    {
        get;
        set;
    } = "";
    
    [ObservableProperty] private LockableProperty<string> _imagePath = DefaultImagePath;

    public string? ImageUrl;
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public Dictionary<string, int> PlayedTime = new(); //ShortDateString() -> PlayedTime, 分钟
    [ObservableProperty] private LockableProperty<string> _name = "";
    [ObservableProperty] private LockableProperty<string> _description = "";
    [ObservableProperty] private LockableProperty<string> _developer = DefaultString;
    [ObservableProperty] private LockableProperty<string> _lastPlay = DefaultString;
    [ObservableProperty] private LockableProperty<string> _expectedPlayTime = DefaultString;
    [ObservableProperty] private LockableProperty<float> _rating = 0;
    [ObservableProperty] private string _savePosition = "本地";
    [ObservableProperty] private string? _exePath;
    [ObservableProperty] private LockableProperty<ObservableCollection<string>> _tags = new();
    [ObservableProperty] private int _totalPlayTime; //单位：分钟
    private bool _isSaveInCloud;
    private RssType _rssType;
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public string?[] Ids = new string?[5]; //magic number: 钦定了一个最大Phraser数目

    public static readonly List<SortKeys> SortKeysList = new ();

    [JsonIgnore] public string? Id
    {
        get => Ids[(int)RssType];

        set
        {
            if (Ids[(int)RssType] != value)
            {
               Ids[(int)RssType] = value;
               OnPropertyChanged(); 
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

    private bool IsSaveInCloud
    {
        set
        {
            _isSaveInCloud = value;
            SavePosition = _isSaveInCloud ? "云端" : "本地";
        }
    }

    public Galgame()
    {
        _tags.Value = new ObservableCollection<string>();
    }

    public Galgame(string path)
    {
        Name = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path + System.IO.Path.DirectorySeparatorChar)) ?? "";
        _tags.Value = new ObservableCollection<string>();
        Path = path;
    }
    
    /// <summary>
    /// 检查游戏文件夹是否存在
    /// </summary>
    public bool CheckExist()
    {
        return Directory.Exists(Path);
    }

    /// <summary>
    /// 更新游戏存档位置（云端/本地）信息
    /// <returns>如果存档在云端返回true，本地返回false</returns>
    /// </summary>
    public bool CheckSavePosition()
    {
        DirectoryInfo directoryInfo = new(Path);
        if (directoryInfo.GetDirectories().Any(IsSymlink))
        {
            IsSaveInCloud = true;
            return true;
        }
        IsSaveInCloud = false;
        return false;
    }

    /// <summary>
    /// 删除游戏文件夹
    /// </summary>
    public void Delete()
    {
        new DirectoryInfo(Path).Delete(true);
    }

    public int CompareTo(Galgame? other)
    {
        if (other is null) return 1;
        foreach (SortKeys keyValue in SortKeysList)
        {
            var result = 0;
            switch (keyValue)
            {
                case SortKeys.Developer:
                    result = string.Compare(_developer.Value!, other._developer.Value, StringComparison.Ordinal);
                    break;
                case SortKeys.Name:
                    result = string.Compare(_name.Value!, other._name.Value, StringComparison.Ordinal);
                    break;
                case SortKeys.Rating:
                    result = _rating.Value.CompareTo(other._rating.Value);
                    break;
                case SortKeys.LastPlay:
                    result = GetTime(_lastPlay.Value!).CompareTo(GetTime(other._lastPlay.Value!));
                    break;
            }
            if (result != 0)
                return -result; //降序
        }
        return 0;
    }

    private long GetTime(string time)
    {
        if (time == DefaultString)
            return 0;
        var tmp = time.Split('/');
        return Convert.ToInt64(tmp[2])+Convert.ToInt64(tmp[1])*31+Convert.ToInt64(tmp[0])*30*12;
    }

    public override bool Equals(object? obj) => obj is Galgame galgame && Path == galgame.Path;
    
    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => Path.GetHashCode();
    
    private static bool IsSymlink(FileSystemInfo fileInfo)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const FileAttributes symlinkAttribute = FileAttributes.ReparsePoint;
            return (fileInfo.Attributes & symlinkAttribute) == symlinkAttribute;
        }
        throw new NotSupportedException("Unsupported operating system.");
    }
    
    /// <summary>
    /// 获取游戏文件夹下的所有exe文件
    /// </summary>
    /// <returns>所有exe文件地址</returns>
    public List<string> GetExes()
    {
        List<string> result = Directory.GetFiles(Path).Where(file => file.ToLower().EndsWith(".exe")).ToList();
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
                if (!process.HasExited)
                {
                    _totalPlayTime++;
                    var now = DateTime.Now.ToShortDateString();
                    if (PlayedTime.ContainsKey(now))
                        PlayedTime[now]++;
                    else
                        PlayedTime.Add(now, 1);
                }
            }
        });
    }
}


public enum SortKeys
{
    Name,
    LastPlay,
    Developer,
    Rating
}
