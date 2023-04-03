// ReSharper disable EnforceIfStatementBraces

using System.Runtime.InteropServices;

using CommunityToolkit.Mvvm.ComponentModel;

using GalgameManager.Services;

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
    
    [ObservableProperty] private string _imagePath = DefaultImagePath;

    public string? ImageUrl;
    
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string _developer = DefaultString;
    [ObservableProperty] private string _lastPlay = DefaultString;
    [ObservableProperty] private string _expectedPlayTime = DefaultString;
    [ObservableProperty] private RssType _rssType = RssType.None;
    [ObservableProperty] private float _rating;
    [ObservableProperty] private string? _id;
    [ObservableProperty] private string _savePosition = "本地";
    [ObservableProperty] private string? _exePath;
    private bool _isSaveInCloud;

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
    /// 更新游戏存档位置信息
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
