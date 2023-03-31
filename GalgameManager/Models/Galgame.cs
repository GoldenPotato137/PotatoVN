// ReSharper disable EnforceIfStatementBraces

using CommunityToolkit.Mvvm.ComponentModel;

using GalgameManager.Services;

namespace GalgameManager.Models;

public class Galgame : ObservableObject
{
    private string _name = "";

    public string Path
    {
        get;
        set;
    } = "";

    public string? ImagePath
    {
        get;
        set;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Description
    {
        get;
        set;
    }

    public string? Developer
    {
        get;
        set;
    }

    public RssType RssType
    {
        get;
        set;
    } = RssType.None;

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

    public override bool Equals(object? obj) => obj is Galgame galgame && Path == galgame.Path;
    
    public override int GetHashCode() => Path.GetHashCode();
}
