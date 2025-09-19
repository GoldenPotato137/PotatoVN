using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using SystemPath = System.IO.Path;


namespace GalgameManager.Models.Sources;


public partial class GalgameFolderSource : GalgameSourceBase
{
    public override GalgameSourceType SourceType =>  GalgameSourceType.LocalFolder;

    public GalgameFolderSource(string path): base(path)
    {
        UpdateRemoveable();
    }

    public GalgameFolderSource()
    {
        
    }

    public override bool IsInSource(string path)
    {
        return Utils.IsChildFolder(Path, path);
    }

    public async override IAsyncEnumerable<(string?, string)> ScanAllGalgames()
    {
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        
        List<string> fileMustContain = new();
        List<string> fileShouldContain = new();
        var searchSubFolder = await localSettings.ReadSettingAsync<bool>(KeyValues.SearchChildFolder);
        var tmp = await localSettings.ReadSettingAsync<string>(KeyValues.GameFolderMustContain);
        if (!string.IsNullOrEmpty(tmp))
            fileMustContain = tmp.Split('\r', '\n').ToList();
        tmp = await localSettings.ReadSettingAsync<string>(KeyValues.GameFolderShouldContain);
        if (!string.IsNullOrEmpty(tmp))
            fileShouldContain = tmp.Split('\r', '\n').ToList();
        
        Queue<(string Path, int Depth)> pathToCheck = new();
        pathToCheck.Enqueue((Path, 0));
        while (pathToCheck.Count > 0)
        {
            var (currentPath, currentDepth) = pathToCheck.Dequeue();
            if (!HasPermission(currentPath))
            {
                yield return (null, "Has No Permission\n");
                continue;
            }
            if (IsGameFolder(currentPath, fileMustContain, fileShouldContain))
            {
                yield return (currentPath, "");
                continue;
            }
            if (!searchSubFolder) continue;
            foreach (var subPath in Directory.GetDirectories(currentPath))
            {
                // 对于属于子源的路径，不应该由当前源来处理（因为如果是批量扫描的，子源会有自己的扫描任务，会处理属于它的路径）
                if (SubSources.Any(s => s is GalgameFolderSource source 
                                        && Utils.ArePathsEqual(source.Path, subPath)))
                    continue;
                pathToCheck.Enqueue((subPath, currentDepth + 1));
            }
        }
    }

    public override bool ApplySearchKey(string searchKey) => Path.ContainX(searchKey);

    public override bool CanChangeScanOnStart => true;
    public override bool CanChangeCheckOnStart => true;
    public override bool CanChangeDetect => true;
    public override bool CanChangeSaveMetaBackup => true;
    public override bool IsGameAddable => true;
    public override bool IsSourceScanable => true;
    public override bool IsDelectable => true;
    public bool RemoveableDrive { get; private set; }
    public bool NetworkDrive { get; private set; }

    /// <summary>
    /// 检查是否具有读取文件夹的权限
    /// </summary>
    private static bool HasPermission(string path)
    {
        try
        {
            Directory.GetFiles(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    /// <summary>
    /// 判断文件夹是否是游戏文件夹
    /// </summary>
    /// <param name="path">文件夹路径</param>
    /// <param name="fileMustContain">必须包含的文件后缀</param>
    /// <param name="fileShouldContain">至少包含一个的文件后缀</param>
    /// <returns></returns>
    private static bool IsGameFolder(string path, List<string> fileMustContain, List<string> fileShouldContain)
    {
        foreach(var file in fileMustContain)
            if (!Directory.GetFiles(path).Any(f => f.ToLower().EndsWith(file)))
                return false;
        var shouldContain = false;
        foreach(var file in fileShouldContain)
            if (Directory.GetFiles(path).Any(f => f.ToLower().EndsWith(file)))
            {
                shouldContain = true;
                break;
            }
        return shouldContain;
    }
    
    public static string GetGalgameName(string path)
    {
        return SystemPath.GetFileName(
            SystemPath.GetDirectoryName(path + SystemPath.DirectorySeparatorChar)) ?? "";
    }

    /// <summary>
    /// 更新这个库的RemoveableDrive属性（以及如果其可移动，自动配置一些属性）
    /// </summary>
    public GalgameFolderSource UpdateRemoveable()
    {
        try
        {
            DriveInfo driver = new(Path);
            RemoveableDrive = driver.DriveType is DriveType.Network or DriveType.Removable;
            if (RemoveableDrive)
            {
                CheckOnStart = false;
                ScanOnStart = true;
                SaveMetaBackup = true;
            }
            NetworkDrive = driver.DriveType == DriveType.Network;
        }
        catch (Exception)
        {
            //ignore
        }
        return this;
    }
}

