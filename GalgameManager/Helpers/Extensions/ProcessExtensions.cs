using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GalgameManager.Helpers;

public static class ProcessExtensions
{
    private const int ProcessQueryLimitedInformation = 0x1000;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags,
        StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public System.Drawing.Point ptMinPosition;
        public System.Drawing.Point ptMaxPosition;
        public System.Drawing.Rectangle rcNormalPosition;
    }
    
    /// <summary>
    /// 窗口是否最小化
    /// </summary>
    public static bool IsMainWindowMinimized(this Process process)
    {
        if (process.MainWindowHandle == IntPtr.Zero) return false;
        WINDOWPLACEMENT placement = new();
        GetWindowPlacement(process.MainWindowHandle, ref placement);
        return placement.showCmd == 2;
    }
    
    public static bool IsMainWindowFocused(this Process process)
    {
        if (process.MainWindowHandle == IntPtr.Zero) return false;
        try
        {
            var foregroundWindow = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
            return process.Id == foregroundProcessId;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 窗口是否处于前台
    /// </summary>
    public static bool IsMainWindowActive(this Process process)
    {
        if (process.MainWindowHandle == IntPtr.Zero) return false;
        try
        {
            Process currentProcess = GetProcessByWindowHandle(GetForegroundWindow());
            return currentProcess.Id == process.Id;
        }
        catch
        {
            return false;
        }
    }

    private static Process GetProcessByWindowHandle(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var processId);
        return Process.GetProcessById((int)processId);
    }

    /// <summary>
    /// 尝试获取进程的可执行文件完整路径。使用<see cref="QueryFullProcessImageName"/>，
    /// 可以跨32/64位进程工作且不需要额外权限；失败（进程已退出、拒绝访问等）时返回null。
    /// </summary>
    public static string? TryGetExecutablePath(this Process process)
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero) return null;
            int capacity = 1024;
            StringBuilder buffer = new(capacity);
            return QueryFullProcessImageName(handle, 0, buffer, ref capacity) ? buffer.ToString() : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero) CloseHandle(handle);
        }
    }
}