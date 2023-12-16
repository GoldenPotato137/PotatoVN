using Windows.Storage;
using GalgameManager.Core.Contracts.Services;

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
    public static void Save(string fileName, object content)
    {
        FileService.Save(_appDataPath, fileName, content);
    }
    
    /// <summary>
    /// 读取某个文件，该文件必须是json格式
    /// </summary>
    public static T? Load<T>(string fileName)
    {
        return FileService.Read<T>(_appDataPath, fileName);
    }
    
    public static void Delete(string fileName)
    {
        FileService.Delete(_appDataPath, fileName);
    }
}