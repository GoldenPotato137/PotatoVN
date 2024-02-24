#nullable enable
using Newtonsoft.Json;

namespace GalgameManager.Core.Contracts.Services;

public interface IFileService
{
    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="fileName">文件名</param>
    /// <param name="settings">序列化设置</param>
    T? Read<T>(string folderPath, string fileName, JsonSerializerSettings? settings = null);
    
    /// <summary>
    /// 读取纯文本数据
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="fileName">文件名</param>
    string ReadWithoutJson(string folderPath, string fileName);

    /// <summary>
    /// 向保存文件队列添加保存任务 <br/>
    /// 被保存的对象会被序列化为JSON来保存
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="fileName">文件名</param>
    /// <param name="content">要保存的内容</param>
    /// <param name="settings">序列化设置</param>
    void Save<T>(string folderPath, string fileName, T content, JsonSerializerSettings? settings = null);
    
    /// <summary>
    /// 立即保存文件 <br/>
    /// 被保存的对象会被序列化为JSON来保存
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="fileName">文件名</param>
    /// <param name="content">要保存的内容</param>
    void SaveNow<T>(string folderPath, string fileName, T content);
    
    /// <summary>
    /// 以字符串的形式保存文件，不会序列化为JSON
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="fileName">文件名</param>
    /// <param name="content">要保存的内容</param>
    void SaveWithoutJson(string folderPath, string fileName, string content);

    /// <summary>
    /// 等待保存队列完成
    /// </summary>
    Task WaitForWriteFinishAsync();

    void Delete(string folderPath, string fileName);
}
