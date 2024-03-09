using Windows.Storage;
using GalgameManager.Core.Contracts.Services;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;


//再给FileService包一层，避免appDataPath满天飞
public static class FileHelper
{
    private static string _appDataPath = string.Empty;
    private static IFileService? _fileService;

    private static IFileService FileService
    {
        get
        {
            if (_fileService is null)
            {
                _appDataPath = ApplicationData.Current.LocalFolder.Path;
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
        FileService.Save(Path.Combine(_appDataPath, subFolder ?? string.Empty), fileName, content, settings);
    }
    
    /// <summary>
    /// 保存纯文本,该函数不会阻塞线程
    /// </summary>
    public static void SaveWithoutJson(string fileName, string content, string? subFolder = null)
    {
        FileService.SaveWithoutJson(Path.Combine(_appDataPath, subFolder ?? string.Empty), fileName, content);
    }
    
    public static void SaveNow<T> (string fileName, T content, string? subFolder = null)
    {
        FileService.SaveNow(Path.Combine(_appDataPath, subFolder ?? string.Empty), fileName, content);
    }
    
    /// <summary>
    /// 读取某个文件，该文件必须是json格式
    /// </summary>
    public static T? Load<T>(string fileName, string? subFolder = null, JsonSerializerSettings? settings = null)
    {
        return FileService.Read<T>(Path.Combine(_appDataPath, subFolder ?? string.Empty), fileName, settings);
    }
    
    /// 读取纯文本
    public static string LoadWithoutJson(string fileName, string? subFolder = null)
    {
        return FileService.ReadWithoutJson(Path.Combine(_appDataPath, subFolder ?? string.Empty), fileName);
    }
    
    public static void Delete(string fileName, string? subFolder = null)
    {
        FileService.Delete(Path.Combine(_appDataPath, subFolder ?? string.Empty), fileName);
    }
    
    public static string GetFullPath(string fileName, string? subFolder = null)
    {
        _ = FileService; //确保初始化
        return Path.Combine(_appDataPath, subFolder ?? string.Empty, fileName);
    }
    
    public static bool Exists(string fileName, string? subFolder = null)
    {
        _ = FileService; //确保初始化
        return File.Exists(Path.Combine(_appDataPath, subFolder ?? string.Empty, fileName));
    }
}