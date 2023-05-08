namespace GalgameManager.Helpers;

public static class FolderOperations
{
    /// <summary>
    /// 将文件夹子目录中的符号链接转换为实际文件夹
    /// </summary>
    /// <param name="folderPath">文件夹地址</param>
    /// <exception cref="ArgumentException">文件夹为nul或空</exception>
    /// <exception cref="DirectoryNotFoundException">文件夹不存在</exception>
    public static void ConvertSymbolicLinksToActual(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            throw new ArgumentException("Folder path cannot be null or empty.");
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        foreach (var subDir in Directory.GetDirectories(folderPath))
        {
            if (new DirectoryInfo(subDir).LinkTarget is { } linkTarget)
            {
                // 创建临时目录
                var tempPath = Path.Combine(folderPath, Path.GetRandomFileName());
                Directory.CreateDirectory(tempPath);
                // 将目标路径的内容复制到临时目录
                foreach (var file in Directory.GetFiles(linkTarget))
                {
                    File.Copy(file, Path.Combine(tempPath, Path.GetFileName(file)), true);
                }
                // 删除符号链接
                Directory.Delete(subDir, true);
                // 将临时目录的内容复制到实际路径，并删除临时目录
                Directory.Move(tempPath, subDir);
            }
        }
    }
    
    /// <summary>
    /// 将文件夹转换为符号链接
    /// </summary>
    /// <param name="sourceFolderPath">原文件夹地址</param>
    /// <param name="targetFolderPath">映射目标地址</param>
    /// <exception cref="ArgumentException">原地址/目标地址为空或null</exception>
    /// <exception cref="DirectoryNotFoundException">原地址不存在</exception>
    public static void CreateSymbolicLink(string sourceFolderPath, string targetFolderPath)
    {
        if (string.IsNullOrEmpty(sourceFolderPath) || string.IsNullOrEmpty(targetFolderPath))
        {
            throw new ArgumentException("Source and target folder paths cannot be null or empty.");
        }
        if (!Directory.Exists(sourceFolderPath))
        {
            throw new DirectoryNotFoundException($"Source folder not found: {sourceFolderPath}");
        }
        // 创建目标文件夹（如果不存在）
        Directory.CreateDirectory(targetFolderPath);
        // 将源文件夹内容移动到目标文件夹
        foreach (var file in Directory.GetFiles(sourceFolderPath))
        {
            File.Move(file, Path.Combine(targetFolderPath, Path.GetFileName(file)), true);
        }
        // 删除原始文件夹
        Directory.Delete(sourceFolderPath, true);
        // 创建符号链接
        Directory.CreateSymbolicLink(sourceFolderPath, targetFolderPath);
    }

    /// <summary>
    /// 复制文件夹
    /// </summary>
    /// <param name="sourcePath">源</param>
    /// <param name="targetPath">目的地</param>
    public static void Copy(string sourcePath, string targetPath)
    {
        if(!Directory.Exists(targetPath))
            Directory.CreateDirectory(targetPath);
        foreach(var file in Directory.GetFiles(sourcePath))
        {
            File.Copy(file, Path.Combine(targetPath, Path.GetFileName(file)), true);
        }
    }
}