using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using Newtonsoft.Json;
using SystemPath = System.IO.Path;


namespace GalgameManager.Models.Sources;


public class GalgameFolderSource : GalgameSourceBase
{
    [JsonIgnore] public bool IsUnpacking;
    public override GalgameSourceType SourceType => GalgameSourceType.LocalFolder;

    public GalgameFolderSource(string path): base(path)
    {
    }

    public GalgameFolderSource()
    {
        
    }

    public override bool IsInSource(string path)
    {
        return SystemPath.GetFullPath(path).StartsWith(SystemPath.GetFullPath(Path)) ;
    }

    public async override IAsyncEnumerable<(Galgame?, string)> ScanAllGalgames()
    {
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        
        List<string> fileMustContain = new();
        List<string> fileShouldContain = new();
        var searchSubFolder = await localSettings.ReadSettingAsync<bool>(KeyValues.SearchChildFolder);
        var maxDepth = searchSubFolder ? await localSettings.ReadSettingAsync<int>(KeyValues.SearchChildFolderDepth) : 1;
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
            }
            if (IsGameFolder(currentPath, fileMustContain, fileShouldContain))
            {
                yield return (new 
                    Galgame(SourceType, GetGalgameName(currentPath), currentPath), "");
            }
        
            if (currentDepth == maxDepth) continue;
            foreach (var subPath in Directory.GetDirectories(currentPath))
                pathToCheck.Enqueue((subPath, currentDepth + 1));
        }
    }
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
}

