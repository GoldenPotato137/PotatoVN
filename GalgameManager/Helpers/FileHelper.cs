using System.Web;
using Windows.Storage;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;


//再给FileService包一层，避免appDataPath满天飞
public static class FileHelper
{
    public static string AppDataPath { get; private set; } = string.Empty;

    private static IFileService? _fileService;

    private static IFileService FileService
    {
        get
        {
            if (_fileService is null)
            {
                AppDataPath = AppStoragePaths.LocalDataPath;
                _fileService = App.GetService<IFileService>();
            }

            return _fileService;
        }
    }

    /// <summary>
    /// 保存某些数据到某个文件，以json格式保存<br/>
    /// 保存不会立刻进行，而是加入保存队列中排队完成<br/>
    /// 该函数不会阻塞线程
    /// </summary>
    public static void Save(string fileName, object content, string? subFolder = null,
        JsonSerializerSettings? settings = null)
    {
        FileService.Save(Path.Combine(AppDataPath, subFolder ?? string.Empty), fileName, content, settings);
    }

    /// <summary>
    /// 保存纯文本,该函数不会阻塞线程
    /// </summary>
    public static void SaveWithoutJson(string fileName, string content, string? subFolder = null)
    {
        FileService.SaveWithoutJson(Path.Combine(AppDataPath, subFolder ?? string.Empty), fileName, content);
    }

    public static void SaveNow<T> (string fileName, T content, string? subFolder = null, bool json = true)
    {
        FileService.SaveNow(Path.Combine(AppDataPath, subFolder ?? string.Empty), fileName, content, json);
    }

    /// <summary>
    /// 读取某个文件，该文件必须是json格式
    /// </summary>
    public static T? Load<T>(string fileName, string? subFolder = null, JsonSerializerSettings? settings = null)
    {
        return FileService.Read<T>(Path.Combine(AppDataPath, subFolder ?? string.Empty), fileName, settings);
    }

    /// 读取纯文本
    public static string LoadWithoutJson(string fileName, string? subFolder = null)
    {
        return FileService.ReadWithoutJson(Path.Combine(AppDataPath, subFolder ?? string.Empty), fileName);
    }

    public static void Delete(string fileName, string? subFolder = null)
    {
        FileService.Delete(Path.Combine(AppDataPath, subFolder ?? string.Empty), fileName);
    }

    public static string GetFullPath(string fileName, string? subFolder = null)
    {
        _ = FileService; //确保初始化
        return Path.Combine(AppDataPath, subFolder ?? string.Empty, fileName);
    }

    public static bool Exists(string fileName, string? subFolder = null)
    {
        _ = FileService; //确保初始化
        return File.Exists(Path.Combine(AppDataPath, subFolder ?? string.Empty, fileName));
    }

    public static async Task<StorageFolder> GetFolderAsync(FolderType folderType)
    {
        StorageFolder root = await StorageFolder.GetFolderFromPathAsync(AppStoragePaths.LocalDataPath);
        if (folderType == FolderType.Root) return root;
        return await root.CreateFolderAsync(folderType.ToString(),
            CreationCollisionOption.OpenIfExists);
    }

    /// <summary>
    /// 去除文件名中不合法的字符
    /// </summary>
    public static string RemoveInvalidFileNameChars(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return invalidChars.Aggregate(fileName, (current, c) => current.Replace(c, '_'));
    }

    /// <summary>
    /// 复制图片到meta文件夹
    /// </summary>
    /// <param name="src">源图片路径</param>
    /// <param name="metaPath">meta文件夹路径</param>
    /// <param name="targetName">目标图片名（不带后缀），如果为null则和原名一致</param>
    /// <returns>复制后的图片的FileInfo，复制失败返回null</returns>
    public static FileInfo? CopyImg(string? src, string metaPath, string? targetName = null)
    {
        if (!Utils.IsImageValid(src) || src is null) return null;
        if (targetName is not null) targetName += Path.GetExtension(src);
        targetName ??= Path.GetFileName(src);
        targetName = targetName.Contains('%') ? HttpUtility.UrlDecode(targetName) : targetName;
        var target = Path.Combine(metaPath, targetName.RemoveInvalidChars());
        if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(src).Length) return new FileInfo(target); //文件已存在且大小相同就不复制
        FolderOperations.CopyEx(src, target, overwrite: true, allowDecrypted: true);
        return new FileInfo(target);
    }

    /// <summary>
    /// 从meta文件夹加载图片路径
    /// </summary>
    /// <param name="target">相对路径</param>
    /// <param name="path">meta文件夹路径</param>
    /// <param name="defaultTarget">默认目标值</param>
    /// <param name="defaultReturn">默认返回值</param>
    /// <returns></returns>
    public static string? LoadImg(string? target, string path, string defaultTarget = Galgame.DefaultImagePath,
        string? defaultReturn = Galgame.DefaultImagePath)
    {
        if (string.IsNullOrEmpty(target) || target == defaultTarget) return defaultReturn;
        var targetPath = Path.GetFullPath(Path.Combine(path, target));
        return File.Exists(targetPath) ? targetPath : defaultReturn;
    }

    /// 自定义背景图在<see cref="FolderType.Background"/>文件夹中的文件名
    public const string CustomBackgroundFileName = "custom.jpg";

    public enum FolderType
    {
        Root,
        Images,
        Plugins,
        Background,
    }
}
