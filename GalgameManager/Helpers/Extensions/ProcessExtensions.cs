using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GalgameManager.Helpers;

public static class ProcessExtensions
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    
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
}