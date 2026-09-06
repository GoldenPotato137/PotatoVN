using System.Diagnostics;

namespace GalgameManager.Helpers;

/// <summary>
/// 基于可执行文件路径的进程探测工具，用于在游戏安装目录内定位真正的游戏进程，
/// 以应对启动器类游戏（引导进程拉起主进程后退出）。
/// </summary>
public static class GameProcessDetector
{
    /// <summary>
    /// 在指定时间窗口内轮询，等待可执行文件路径位于游戏目录下的进程出现，返回最佳候选。
    /// </summary>
    /// <param name="directory">游戏安装目录</param>
    /// <param name="timeout">最长等待时间</param>
    /// <param name="excludeProcessIds">要排除的进程Id</param>
    public static async Task<Process?> WaitForProcessInDirectoryAsync(string directory, TimeSpan timeout,
        IReadOnlyCollection<int>? excludeProcessIds = null)
    {
        string prefix = GetDirectoryPrefix(directory);
        DateTime deadline = DateTime.UtcNow + timeout;
        do
        {
            Process? candidate = FindBestProcessInDirectory(prefix, excludeProcessIds);
            if (candidate is not null) return candidate;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);
        return null;
    }

    /// <summary>
    /// 获取当前可执行文件路径位于目录前缀下的所有进程Id。
    /// </summary>
    public static HashSet<int> GetProcessIdsInDirectory(string directoryPrefix)
    {
        HashSet<int> result = [];
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (IsInDirectory(process, directoryPrefix)) result.Add(process.Id);
            }
            catch
            {
                // ignored
            }
            finally
            {
                process.Dispose();
            }
        }
        return result;
    }

    /// <summary>
    /// 枚举可执行文件路径位于目录前缀下的进程，返回最佳候选（优先有主窗口的，其次启动时间最晚的）。
    /// </summary>
    public static Process? FindBestProcessInDirectory(string directoryPrefix,
        IReadOnlyCollection<int>? excludeProcessIds = null)
    {
        Process? best = null;
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (excludeProcessIds?.Contains(process.Id) == true ||
                    !IsInDirectory(process, directoryPrefix))
                {
                    process.Dispose();
                    continue;
                }
                if (best is null || IsBetter(process, best))
                {
                    best?.Dispose();
                    best = process;
                }
                else
                    process.Dispose();
            }
            catch
            {
                process.Dispose();
            }
        }
        return best;

        static bool IsBetter(Process candidate, Process current)
        {
            bool candidateHasWindow = HasWindow(candidate);
            bool currentHasWindow = HasWindow(current);
            if (candidateHasWindow != currentHasWindow) return candidateHasWindow;
            try
            {
                return candidate.StartTime > current.StartTime;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 将目录规范化为带结尾分隔符的完整路径前缀，避免把兄弟目录（如game2）误判为子路径。
    /// </summary>
    public static string GetDirectoryPrefix(string directory)
    {
        string fullPath = Path.GetFullPath(directory);
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
               Path.DirectorySeparatorChar;
    }

    public static bool IsAlive(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public static bool HasWindow(Process process)
    {
        try
        {
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    public static int SafeGetId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return -1;
        }
    }

    private static bool IsInDirectory(Process process, string directoryPrefix)
    {
        if (process.HasExited) return false;
        string? path = process.TryGetExecutablePath();
        return path is not null && path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
