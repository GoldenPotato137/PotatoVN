// ReSharper disable EnforceIfStatementBraces

using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Models;

public class Galgame : ObservableObject
{
    private string _name = "";

    public string Path
    {
        get;
    } = "";

    public string? ImagePath
    {
        get;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Description
    {
        get;
        internal set;
    }

    public string? Developer
    {
        get;
        internal set;
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
}
