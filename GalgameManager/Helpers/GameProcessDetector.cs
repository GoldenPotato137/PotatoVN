using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GalgameManager.Helpers;

/// <summary>
/// 基于可执行文件路径的进程探测工具，用于在游戏安装目录内定位真正的游戏进程，
/// 以应对启动器类游戏（引导进程拉起主进程后退出）。
/// </summary>
public static class GameProcessDetector
{
    private delegate bool EnumWindowsProc(nint windowHandle, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint windowHandle, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint windowHandle, StringBuilder className, int maxCount);

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
                // 单个进程拒绝访问时跳过，继续采集其余进程。
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
        IReadOnlyCollection<int>? excludeProcessIds = null,
        string? requiredProcessName = null)
    {
        Process? best = null;
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (excludeProcessIds?.Contains(process.Id) == true ||
                    (!string.IsNullOrWhiteSpace(requiredProcessName) &&
                     !string.Equals(process.ProcessName, requiredProcessName,
                         StringComparison.OrdinalIgnoreCase)) ||
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
    /// 获取安装目录内本次启动新出现进程的主要可见窗口。优先返回前台窗口，
    /// 没有前台窗口时返回面积最大的窗口，用于在正式进程附着前保存启动窗口历史。
    /// </summary>
    public static GameWindowSnapshot? TryGetPrimaryWindowSnapshotInDirectory(string directoryPrefix,
        IReadOnlyCollection<int>? excludeProcessIds = null)
    {
        nint foregroundWindow = GetForegroundWindow();
        GameWindowSnapshot? foreground = null;
        GameWindowSnapshot? largest = null;
        long largestArea = -1;
        HashSet<int> acceptedProcessIds = [];
        HashSet<int> rejectedProcessIds = excludeProcessIds is null ? [] : new HashSet<int>(excludeProcessIds);
        EnumWindowsProc callback = (windowHandle, _) =>
        {
            if (!IsWindowVisible(windowHandle) ||
                GetWindowThreadProcessId(windowHandle, out uint rawProcessId) == 0 || rawProcessId == 0 ||
                !GetWindowRect(windowHandle, out NativeRect rect)) return true;

            int processId = unchecked((int)rawProcessId);
            if (rejectedProcessIds.Contains(processId)) return true;
            if (!acceptedProcessIds.Contains(processId))
            {
                try
                {
                    using Process process = Process.GetProcessById(processId);
                    if (!IsInDirectory(process, directoryPrefix))
                    {
                        rejectedProcessIds.Add(processId);
                        return true;
                    }
                    acceptedProcessIds.Add(processId);
                }
                catch
                {
                    rejectedProcessIds.Add(processId);
                    return true;
                }
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return true;

            StringBuilder title = new(512);
            StringBuilder className = new(256);
            _ = GetWindowText(windowHandle, title, title.Capacity);
            _ = GetClassName(windowHandle, className, className.Capacity);
            GameWindowSnapshot snapshot = new(processId, windowHandle, className.ToString(), title.ToString(), width,
                height);
            if (windowHandle == foregroundWindow) foreground = snapshot;
            long area = (long)width * height;
            if (area > largestArea)
            {
                largestArea = area;
                largest = snapshot;
            }
            return true;
        };

        try
        {
            _ = EnumWindows(callback, 0);
            return foreground ?? largest;
        }
        catch
        {
            return null;
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
        int processId = SafeGetId(process);
        try
        {
            return !process.HasExited;
        }
        catch
        {
            // 管理员或受保护进程可能拒绝 Process.HasExited 请求的句柄，
            // 即使进程仍然存在。此时回退到系统进程快照，不能把拒绝访问当成退出。
            return IsProcessIdPresent(processId);
        }
    }

    /// <summary>
    /// 不打开目标进程查询句柄，直接从系统进程快照判断 PID 是否存在。
    /// 即使 <see cref="Process.HasExited"/> 或 <see cref="Process.WaitForExitAsync(CancellationToken)"/> 被拒绝也可使用。
    /// </summary>
    public static bool IsProcessIdPresent(int processId)
    {
        if (processId <= 0) return false;
        try
        {
            Process[] candidates = Process.GetProcesses();
            try
            {
                foreach (Process candidate in candidates)
                    if (candidate.Id == processId) return true;
                return false;
            }
            finally
            {
                foreach (Process candidate in candidates) candidate.Dispose();
            }
        }
        catch
        {
            // 快照采集失败不能证明某个具体进程仍然存活。
        }
        return false;
    }

    /// <summary>
    /// 等待进程退出；Windows 拒绝为管理员进程授予等待或查询句柄时，
    /// 回退到 PID 快照轮询。
    /// </summary>
    public static async Task WaitForExitSafelyAsync(Process process, CancellationToken cancellationToken = default)
    {
        int processId = SafeGetId(process);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // ShellExecute 代理和管理员目标进程可能不提供可等待句柄。
        }

        while (IsProcessIdPresent(processId))
            await Task.Delay(1000, cancellationToken);
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

    /// <summary>
    /// 获取进程位于前台的可见顶层窗口；没有前台窗口时回退到面积最大的可见窗口。
    /// 这里通过 user32 枚举窗口，不打开进程查询句柄，因此仍可观察管理员权限游戏。
    /// </summary>
    public static GameWindowSnapshot? TryGetPrimaryWindowSnapshot(Process process)
    {
        int processId = SafeGetId(process);
        if (processId <= 0) return null;

        nint foregroundWindow = GetForegroundWindow();
        GameWindowSnapshot? foreground = null;
        GameWindowSnapshot? largest = null;
        long largestArea = -1;
        EnumWindowsProc callback = (windowHandle, _) =>
        {
            if (!IsWindowVisible(windowHandle)) return true;
            GetWindowThreadProcessId(windowHandle, out uint windowProcessId);
            if (windowProcessId != (uint)processId || !GetWindowRect(windowHandle, out NativeRect rect)) return true;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return true;

            StringBuilder title = new(512);
            StringBuilder className = new(256);
            _ = GetWindowText(windowHandle, title, title.Capacity);
            _ = GetClassName(windowHandle, className, className.Capacity);
            GameWindowSnapshot snapshot = new(processId, windowHandle, className.ToString(), title.ToString(), width,
                height);
            if (windowHandle == foregroundWindow) foreground = snapshot;
            long area = (long)width * height;
            if (area > largestArea)
            {
                largestArea = area;
                largest = snapshot;
            }
            return true;
        };

        try
        {
            _ = EnumWindows(callback, 0);
            return foreground ?? largest;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 当前台窗口所属进程的可执行文件位于安装目录内时返回该进程。
    /// 返回的 <see cref="Process"/> 实例由调用方负责释放。
    /// </summary>
    public static Process? TryGetForegroundProcessInDirectory(string directoryPrefix)
    {
        nint windowHandle = GetForegroundWindow();
        if (windowHandle == 0 || GetWindowThreadProcessId(windowHandle, out uint processId) == 0 || processId == 0)
            return null;

        try
        {
            Process process = Process.GetProcessById((int)processId);
            if (IsInDirectory(process, directoryPrefix)) return process;
            process.Dispose();
        }
        catch
        {
            // 从查询 PID 到打开进程之间，前台窗口可能已经消失。
        }
        return null;
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

    /// <summary>
    /// 判断进程的可执行文件是否位于指定目录前缀内。
    /// </summary>
    public static bool IsProcessInDirectory(Process process, string directoryPrefix) =>
        IsInDirectory(process, directoryPrefix);

    private static bool IsInDirectory(Process process, string directoryPrefix)
    {
        if (!IsAlive(process)) return false;
        string? path = process.TryGetExecutablePath();
        return path is not null && path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
