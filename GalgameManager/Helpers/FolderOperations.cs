using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
            ConvertSymbolicLinkToActual(subDir);
    }

    /// <summary>
    /// 将符号链接转换为实际文件夹
    /// </summary>
    public static void ConvertSymbolicLinkToActual(string path)
    {
        if (Directory.Exists(path) == false) return;
        if (new DirectoryInfo(path).Parent is null) return;
        if (new DirectoryInfo(path).LinkTarget is not { } linkTarget) return;
        // 创建临时目录
        var tempPath = Path.Combine(new DirectoryInfo(path).Parent!.FullName, Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);
        // 将目标路径的内容复制到临时目录
        Copy(linkTarget, tempPath);
        // 删除符号链接
        Directory.Delete(path, true);
        // 将临时目录的内容复制到实际路径，并删除临时目录
        Directory.Move(tempPath, path);
    }

    /// <summary>
    /// 将文件夹转换为符号链接
    /// </summary>
    /// <param name="sourcePath">原文件/文件夹地址</param>
    /// <param name="targetPath">映射目标地址</param>
    /// <exception cref="ArgumentException">原地址/目标地址为空或null</exception>
    /// <exception cref="DirectoryNotFoundException">原地址不存在</exception>
    public static void ConvertFolderOrFileToSymbolicLink(string sourcePath, string targetPath)
    {
        var isSrcFolder = File.GetAttributes(sourcePath).HasFlag(FileAttributes.Directory);
        
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
        {
            throw new ArgumentException("Source and target folder paths cannot be null or empty.");
        }
        if (isSrcFolder && !Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source folder not found: {sourcePath}");
        }
        if (!isSrcFolder && !File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }
        // 1. 创建目标文件夹（如果不存在）        
        // 2. 将源文件夹内容移动到目标文件夹
        // 3. 删除原始文件夹
        if (isSrcFolder)
        {
            Directory.CreateDirectory(targetPath);
            Copy(sourcePath, targetPath);
            Directory.Delete(sourcePath, true);
        } 
        else
        {
            var targetFolder = Path.GetDirectoryName(targetPath);
            if (targetFolder is null)
            {
                throw new ArgumentException($"Invalid path: {targetPath}");
            }
            Directory.CreateDirectory(targetFolder);
            File.Move(sourcePath, targetPath, true);
        }
        // 4. 创建符号链接
        CreateSymbolicLink(sourcePath, targetPath);
    }

    /// <summary>
    /// 创造符号链接
    /// </summary>
    /// <param name="sourceFolderPath">原地址</param>
    /// <param name="targetFolderPath">引用地址</param>
    public static void CreateSymbolicLink(string sourceFolderPath, string targetFolderPath)
    {
        var isTargetFolder = File.GetAttributes(targetFolderPath).HasFlag(FileAttributes.Directory);

        var linkPath = sourceFolderPath;
        var realPath = targetFolderPath;

        var args = $"/c mklink {(isTargetFolder ? "/D" : "")} \"{linkPath}\" \"{realPath}\"";

        ProcessStartInfo processInfo = new()
        {
            Verb = "runas",
            FileName = "cmd.exe",
            Arguments = args,
            UseShellExecute = true,
        };
        Process? process = Process.Start(processInfo);
        if (process is null) throw new Exception("Failed to start cmd.exe");
        process.WaitForExit();

        if (isTargetFolder && Directory.Exists(sourceFolderPath) == false)
            throw new Exception("Failed to create symbolic link.");
        if (!isTargetFolder && Path.Exists(sourceFolderPath) == false)
            throw new Exception("Failed to create symbolic link.");
    }

    /// <summary>
    /// 复制文件夹
    /// </summary>
    /// <param name="sourcePath">源</param>
    /// <param name="targetPath">目的地</param>
    /// <param name="onCopy">复制文件前触发</param>
    public static void Copy(string sourcePath, string targetPath, Action<FileInfo>? onCopy = null)
    {
        if(!Directory.Exists(targetPath))
            Directory.CreateDirectory(targetPath);
        foreach(var file in Directory.GetFiles(sourcePath))
        {
            onCopy?.Invoke(new FileInfo(file));
            File.Copy(file, Path.Combine(targetPath, Path.GetFileName(file)), true);
        }
        foreach(var subDir in Directory.GetDirectories(sourcePath))
            Copy(subDir, Path.Combine(targetPath, Path.GetFileName(subDir)), onCopy);
    }

    /// <summary>
    /// 删除文件夹，其中的符号链接将被删除（但不删除符号链接指向的文件）
    /// </summary>
    /// <param name="path"></param>
    /// <param name="onDelete"></param>
    /// <exception cref="DirectoryNotFoundException">path不存在</exception>
    public static void Delete(string path, Action<FileInfo>? onDelete = null)
    {
        if (IsSymbolicLink(path)) // 放在存在性检查前：目标已不存在的失效链接也只删除链接本身
        {
            Directory.Delete(path); // 只删除链接本身，不进入链接指向的文件夹
            return;
        }
        if(!Directory.Exists(path)) throw new DirectoryNotFoundException();
        foreach (var file in Directory.GetFiles(path))
        {
            FileInfo info = new(file);
            onDelete?.Invoke(info);
            if (info.IsReadOnly && info.LinkTarget is null) info.IsReadOnly = false; // 只读文件无法直接删除
            info.Delete();
        }
        // 先自底向上删完子文件夹，最后删除自身（非递归的Directory.Delete要求文件夹已为空）
        foreach (var dir in Directory.GetDirectories(path))
            Delete(dir, onDelete);
        Directory.Delete(path);
    }
    
    /// <summary>
    /// 判断文件夹是否为符号链接
    /// </summary>
    public static bool IsSymbolicLink(string path)
    {
        return new DirectoryInfo(path).LinkTarget is not null;
    }

    public static DirectoryInfo CreateSubdirectoryEx(this DirectoryInfo directory, string subdirectory,
        bool overWrite = false)
    {
        if (overWrite && Directory.Exists(Path.Combine(directory.FullName, subdirectory)))
            Directory.Delete(Path.Combine(directory.FullName, subdirectory), true);
        return directory.CreateSubdirectory(subdirectory);
    }

    #region COPY_EX

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CopyFileEx(
        string lpExistingFileName,
        string lpNewFileName,
        IntPtr lpProgressRoutine,   // 进度回调，用不上可填 0
        IntPtr lpData,              // 传给回调的数据
        ref bool pbCancel,          // 复制过程中可以设为 true 来取消
        CopyFileFlags dwCopyFlags);
    
    [Flags]
    private enum CopyFileFlags : uint
    {
        NONE                                    = 0x00000000,
        COPY_FILE_FAIL_IF_EXISTS                = 0x00000001, // 不覆盖
        COPY_FILE_RESTARTABLE                   = 0x00000002,
        COPY_FILE_OPEN_SOURCE_FOR_WRITE         = 0x00000004,
        COPY_FILE_ALLOW_DECRYPTED_DESTINATION   = 0x00000008  // 允许非加密目标
        // 其余标志见 MSDN
    }
    
    /// <summary>
    /// 复制文件，支持：
    /// 1. 选择是否覆盖已存在目标文件
    /// 2. 源文件为 EFS 加密时，可选择让目标以“非加密”形式保存
    /// </summary>
    /// <param name="src">源文件完整路径</param>
    /// <param name="dst">目标文件完整路径</param>
    /// <param name="overwrite">true = 覆盖；false = 若目标已存在则抛异常</param>
    /// <param name="allowDecrypted">
    /// true  = 若源文件加密，允许目标写成“未加密”；  
    /// false = 保持与源一致（默认）
    /// </param>
    public static void CopyEx(
        string src,
        string dst,
        bool overwrite = true,
        bool allowDecrypted = false)
    {
        src = AsExtendedPath(src);
        dst = AsExtendedPath(dst);
        CopyFileFlags flags = CopyFileFlags.NONE;
        if (!overwrite) flags |= CopyFileFlags.COPY_FILE_FAIL_IF_EXISTS;
        if (allowDecrypted) flags |= CopyFileFlags.COPY_FILE_ALLOW_DECRYPTED_DESTINATION;
        var cancel = false;
        if (!CopyFileEx(src, dst, IntPtr.Zero, IntPtr.Zero, ref cancel, flags))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static string AsExtendedPath(string path)
    {
        if (path.StartsWith(@"\\?\")) return path;          // 已经是扩展路径  
        if (path.StartsWith(@"\\"))
            return @"\\?\UNC\" + path.Substring(2);         // UNC → \\?\UNC\Server\Share\...
        return @"\\?\" + path;                              // 本地盘符 → \\?\C:\...
    }
    
    #endregion
    
}