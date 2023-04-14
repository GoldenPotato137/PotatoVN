// ReSharper disable EnforceIfStatementBraces

using System.Runtime.InteropServices;

using CommunityToolkit.Mvvm.ComponentModel;

using GalgameManager.Services;

using Newtonsoft.Json;

namespace GalgameManager.Models;

public partial class Galgame : ObservableObject
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
    
    [ObservableProperty] private LockableProperty<string> _name = "";
    [ObservableProperty] private LockableProperty<string> _description = "";
    [ObservableProperty] private LockableProperty<string> _developer = DefaultString;
    [ObservableProperty] private LockableProperty<string> _lastPlay = DefaultString;
    [ObservableProperty] private LockableProperty<string> _expectedPlayTime = DefaultString;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Id))] private RssType _rssType = RssType.None;
    [ObservableProperty] private LockableProperty<float> _rating = 0;
    [ObservableProperty] private string _savePosition = "本地";
    [ObservableProperty] private string? _exePath;
    private bool _isSaveInCloud;
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public string[] Ids = new string[5]; //magic number: 钦定了一个最大Phraser数目

    [JsonIgnore] public string Id
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
    }

    public Galgame(string path)
    {
        Name = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path + System.IO.Path.DirectorySeparatorChar)) ?? "";
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
    /// </summary>
    public void CheckSavePosition()
    {
        var directoryInfo = new DirectoryInfo(Path);
        if (directoryInfo.GetDirectories().Any(IsSymlink))
        {
            IsSaveInCloud = true;
            return;
        }
        IsSaveInCloud = false;
    }

    /// <summary>
    /// 删除游戏文件夹
    /// </summary>
    public void Delete()
    {
        new DirectoryInfo(Path).Delete(true);
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
    
    public List<string> GetExes()
    {
        var result = Directory.GetFiles(Path).Where(file => file.ToLower().EndsWith(".exe")).ToList();
        return result;
    }
}
