using System.Diagnostics;
using System.Runtime.InteropServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;

namespace GalgameManager.Models.BgTasks;

public class QuickMinimizeTask : BgTaskBase
{
    public Galgame? Galgame { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }

    private Process? _process;
    private bool _wasPressed;

    public QuickMinimizeTask() { }

    public QuickMinimizeTask(Galgame game, Process process)
    {
        Galgame = game;
        _process = process;
        ProcessName = process.ProcessName;
        ProcessId = process.Id;
    }

    protected override Task RecoverFromJsonInternal()
    {
        try
        {
            _process = Process.GetProcessById(ProcessId);
        }
        catch
        {
            _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        }
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        if (Galgame is null || _process is null) return;

        ChangeProgress(0, 1, "QuickMinimizeTask_Running");

        while (_process is { HasExited: false })
        {
            try
            {
                List<int> hotkeys = App.GetService<ILocalSettingsService>()
                    .ReadSettingAsync<List<int>>(KeyValues.QuickMinimizeHotkeys).Result ?? [];

                if (hotkeys.Count > 0 && IsHotkeyPressed(hotkeys))
                {
                    if (!_wasPressed)
                    {
                        var didMinimize = ToggleMinimizeProcess(_process);
                        if (didMinimize)
                            App.SetWindowMode(WindowMode.Minimize); // 仅当执行最小化时才最小化软件窗口
                        _wasPressed = true;
                    }
                }
                else
                {
                    _wasPressed = false;
                }
            }
            catch
            {
                // ignore and continue
            }

            await Task.Delay(50);
        }

        ChangeProgress(1, 1, string.Empty, false);
    }

    // 返回 true 表示执行了“最小化”，返回 false 表示执行了“恢复”
    private static bool ToggleMinimizeProcess(Process process)
    {
        try
        {
            List<IntPtr> windows = [];
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == (uint)process.Id)
                {
                    IntPtr owner = GetAncestor(hWnd, GA_ROOTOWNER);
                    if (owner != IntPtr.Zero) hWnd = owner;
                    if (!windows.Contains(hWnd)) windows.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);

            if (windows.Count == 0) return false;

            bool anyIconic = windows.Any(h => IsIconic(h));
            if (anyIconic)
            {
                // 恢复所有最小化窗口（软件窗口保持不变）
                bool focused = false;
                foreach (IntPtr hWnd in windows)
                {
                    ShowWindowAsync(hWnd, SW_RESTORE);
                    PostMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
                    if (!focused)
                    {
                        SetForegroundWindow(hWnd);
                        focused = true;
                    }
                }
                return false; // 执行的是恢复
            }
            
            // 最小化所有窗口
            foreach (IntPtr hWnd in windows)
            {
                // 1) 优先异步最小化
                ShowWindowAsync(hWnd, SW_MINIMIZE);
                // 2) 发送系统命令最小化（某些窗口更可靠）
                PostMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
                // 3) 强制最小化作为兜底
                ShowWindow(hWnd, SW_FORCEMINIMIZE);
            }
            return true; // 执行的是最小化
        }
        catch
        {
            // ignore
        }
        return false;
    }

    private static bool IsHotkeyPressed(List<int> keys)
    {
        // 所有按键同时按下才算触发
        foreach (int key in keys)
        {
            short state = GetAsyncKeyState(key);
            if ((state & 0x8000) == 0) return false;
        }
        return true;
    }

    public override string Title => "QuickMinimizeTask";

    private const int SW_MINIMIZE = 6;
    private const int SW_FORCEMINIMIZE = 11;
    private const int SW_RESTORE = 9;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_RESTORE = 0xF120;
    private const uint GA_ROOTOWNER = 3;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
}


