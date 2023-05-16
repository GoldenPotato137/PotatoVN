namespace GalgameManager.Core.Contracts.Services;

public interface IFileService
{
    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="fileName">文件名</param>
    /// <param name="useJonSerializer">是否使用 JsonSerializer，若为否则使用JsonConvert</param>
    T Read<T>(string folderPath, string fileName, bool useJonSerializer = false);

    /// <summary>
    /// 保存数据
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="fileName">文件名</param>
    /// <param name="content">要保存的内容</param>
    /// <param name="useJonSerializer">是否使用 JsonSerializer，若为否则使用JsonConvert</param>
    Task Save<T>(string folderPath, string fileName, T content, bool useJonSerializer = false);

    void Delete(string folderPath, string fileName);
}
