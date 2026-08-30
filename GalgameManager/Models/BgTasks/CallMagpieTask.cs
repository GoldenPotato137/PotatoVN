using System.Diagnostics;
using System.Runtime.InteropServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using WindowsInput;
using WindowsInput.Native;

namespace GalgameManager.Models.BgTasks;

public class CallMagpieTask : BgTaskBase
{
    private const int MaxRetryCount = 15; // 最大重试次数
    public Galgame? Galgame { get; set; }
    public string ProcessName { get; set; } = null!;
    public bool HashFinished { get; set; }
    private Process? _process;
    private GameRuntimeProcessRelay? _processRelay;

    public CallMagpieTask() { } // 仅用于后台任务序列化恢复

    public CallMagpieTask(Galgame game, Process process)
        : this(game, process, null)
    {
    }

    public CallMagpieTask(Galgame game, Process process, GameRuntimeProcessRelay? processRelay)
    {
        Galgame = game;
        _process = process;
        _processRelay = processRelay;
        try
        {
            ProcessName = process.ProcessName;
        }
        catch
        {
            // 短命启动器退出后仍等待计时任务发布正式游戏进程。
        }
    }
    
    protected async override Task RecoverFromJsonInternal()
    {
        await Task.CompletedTask;
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
    }

    protected async override Task RunInternal()
    {
        if (Galgame is null) throw new PvnException("Galgame is null");
        if (_process is null)
        {
            _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            if (_process is null) throw new PvnException("Process not found");
        }
        if (_processRelay is not null)
        {
            Process? confirmed = await _processRelay.WaitForConfirmationAsync();
            if (confirmed is null || !GameProcessDetector.IsAlive(confirmed) || HashFinished)
            {
                ChangeProgress(1, 1, string.Empty, false);
                return;
            }
            _process = confirmed;
        }
        var magpiePath = await App.GetService<ILocalSettingsService>().ReadSettingAsync<string>(KeyValues.MagpiePath);
        if (string.IsNullOrEmpty(magpiePath)) throw new PvnException("CallMagpieTask_NoMagpiePath".GetLocalized());
        ChangeProgress(0, 1, "CallMagpieTask_LaunchingMagpie".GetLocalized());
        await MagpieHelper.LaunchMagpieAsync(magpiePath);

        if (!GameProcessDetector.IsAlive(_process) || HashFinished) return;
        try
        {
            ProcessName = _process.ProcessName;
        }
        catch
        {
            // 受保护进程可能拒绝读取名称，仍继续尝试通过窗口调用 Magpie。
        }
        for (var retry = 0; retry < MaxRetryCount && !HashFinished; retry++)
        {
            try
            {
                ChangeProgress(0, 1, "CallMagpieTask_Trying".GetLocalized(retry));
                MagpieHelper.ExecuteMagpie(_process);
                HashFinished = true;
            }
            catch (MagpieHelper.MagpieNoMainWinException)
            {
                // 主窗口还没出现，等待一会儿
                await Task.Delay(1000);
            }
        }
        ChangeProgress(1 ,1, "CallMagpieTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
    }

    public override string Title => "CallMagpieTask_Title".GetLocalized();
}

public static class MagpieHelper
{
    // 用于将窗口置于前台的 API
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    // 用于显示窗口 (如果窗口被最小化或隐藏)
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9; // 用于恢复窗口

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    public static bool FocusProcessWindow(Process process)
    {
        if (process.MainWindowHandle == IntPtr.Zero) throw new PvnException("Main window handle is null");

        var hWnd = process.MainWindowHandle;

        // 有时候，仅仅 SetForegroundWindow 可能不够，特别是当调用进程不是前台进程时。
        // 一种更可靠的方法是临时将当前线程的输入处理附加到目标窗口的线程。
        var currentThreadId = GetCurrentThreadId();
        var foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);

        if (currentThreadId != foregroundThreadId) AttachThreadInput(currentThreadId, foregroundThreadId, true);

        // 显示窗口 (如果它被最小化了)
        ShowWindow(hWnd, SW_RESTORE);

        // 将窗口置于前台
        var success = SetForegroundWindow(hWnd);
        if (currentThreadId != foregroundThreadId)
            AttachThreadInput(currentThreadId, foregroundThreadId, false); // 分离线程输入
        if (!success)
            throw new PvnException("WindowHelper_FocusProcessWindow_Failed".GetLocalized(Marshal.GetLastWin32Error()));
        Thread.Sleep(200); // 等待一会确保焦点命令执行完成
        return success;
    }

    // 需要额外声明 GetWindowThreadProcessId
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

    public static void ExecuteMagpie(Process process)
    {
        if (process.MainWindowHandle == IntPtr.Zero) throw new MagpieNoMainWinException("MainWindowHandle is zero");
        if (!FocusProcessWindow(process)) throw new PvnException("FocusProcessWindow failed");
        List<int> shortcuts = App.GetService<ILocalSettingsService>()
            .ReadSettingAsync<List<int>>(KeyValues.MagpieHotkeys).Result ?? [];
        InputSimulator keyboard = new();
        // 依次按下快捷键。
        foreach (var shortcut in shortcuts)
            keyboard.Keyboard.KeyDown((VirtualKeyCode)shortcut);
        // 按相反顺序释放快捷键。
        for (var i = shortcuts.Count - 1; i >= 0; i--)
            keyboard.Keyboard.KeyUp((VirtualKeyCode)shortcuts[i]);
    }

    public static async Task LaunchMagpieAsync(string magpiePath)
    {
        Process[] processes = Process.GetProcesses();
        if (processes.Any(process => process.ProcessName.Equals("Magpie", StringComparison.OrdinalIgnoreCase))) return;
        ProcessStartInfo startInfo = new(magpiePath)
        {
            UseShellExecute = true,
            // WindowStyle = ProcessWindowStyle.Normal
        };
        Process.Start(startInfo);
        
        // 每隔200毫秒检测一次，最多等待5秒
        const int checkIntervalMs = 200;
        const int maxWaitTimeMs = 5000;
        var totalWaitTime = 0;
        
        while (totalWaitTime < maxWaitTimeMs)
        {
            await Task.Delay(checkIntervalMs);
            totalWaitTime += checkIntervalMs;
            
            // 检查 Magpie 进程是否已启动
            Process[] currentProcesses = Process.GetProcesses();
            if (currentProcesses.Any(process => process.ProcessName.Equals("Magpie", StringComparison.OrdinalIgnoreCase)))
            {
                await Task.Delay(500); // 等待500毫秒以确保焦点切换到游戏上
                return; // Magpie 已启动，立即返回
            }
        }
    }

    public class MagpieNoMainWinException(string msg) : PvnException(msg);
}
