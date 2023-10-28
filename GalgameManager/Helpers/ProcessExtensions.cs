using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GalgameManager.Helpers;

public static class ProcessExtensions
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public System.Drawing.Point ptMinPosition;
        public System.Drawing.Point ptMaxPosition;
        public System.Drawing.Rectangle rcNormalPosition;
    }


    public static bool IsMainWindowMinimized(this Process process)
    {
        if (process.MainWindowHandle == IntPtr.Zero) return false;
        WINDOWPLACEMENT placement = new();
        GetWindowPlacement(process.MainWindowHandle, ref placement);
        return placement.showCmd == 2;
    }
}