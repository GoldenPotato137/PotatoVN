using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.JobObjects;
using Windows.Win32.System.Threading;

namespace GalgameManager.Helpers;

public record StartGameOptions
{
    public string? WorkingDirectory { get; init; }
    public bool CreateNoWindow { get; init; }
}

public class GameSessionManager
{
    private const int ProcessMaxWaitSec = 60; //(手动指定游戏进程)等待游戏进程启动的最大时间

    // 委托：当检测到新进程时触发
    public event Action<int>? OnProcessStarted;
    // 委托：当检测到进程退出时触发
    public event Action<int>? OnProcessExited;
    // 委托：当游戏会话完全结束（所有相关进程都退出）时触发
    public event Action? OnGameSessionEnded;

    private readonly ConcurrentDictionary<int, Process> _runningProcesses = new();
    private bool _isExternalInit = false;
    private Process _latestProcess = null!;
    private Process? _gameProcess = null;

    public Process GetLatestProcess() => _latestProcess;

    public Process? GetGameProcess() => _gameProcess;

    public void StartGame(Process process)
    {
        process.Start();
        _latestProcess = process;
        _runningProcesses[process.Id] = process;
        _isExternalInit = true;
    }

    public void StartGame(string executablePath, string arguments, StartGameOptions options)
    {
        if (executablePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // 防呆
            throw new NotSupportedException("CreateProcess 不支持 .lnk 文件。请先解析快捷方式获取真实 Exe 路径。");
        }
        Task.Run(() => MonitorLogic(executablePath, arguments, options));
    }

    private async Task<Process?> WaitGameCSharp(string? processName, string? exePath, string? exeArgs)
    {
        if (processName is not null)
        {
            await Task.Delay(1000 * 2); //有可能引导进程和游戏进程是一个名字
            return await WaitForProcessStartAsync(processName);
        }

        if (!string.IsNullOrEmpty(exeArgs))
        {
            //启动的进程和游戏进程不是同一个进程，需要知道到底启动什么进程
            await Task.Delay(1000 * 2);
            if (TryGetProcessFromName(exePath) is { } p) // 尝试根据游戏可执行文件名获取进程
            {
                return p;
            }
        }

        return null;
    }

    private async Task<Process?> WaitGameNative(string? processName, string? exePath, string? exeArgs)
    {
        // 对于 Native 方法，无条件认为 2s 延迟后，最新的 Process 就是游戏进程。
        await Task.Delay(1000 * 2);
        Process ret = _latestProcess;
        // 等待匹配 processName 的 latestProcess 出现
        if (processName is not null)
        {
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(1000 * 2);
                Process? p = await WaitForProcessStartAsync(processName);
                if (p is not null && p.Id == _latestProcess?.Id)
                {
                    ret = _latestProcess;
                    break;
                }
            }
        }

        return ret;
    }

    public async Task<Process?> WaitGame(string? processName, string? exePath, string? exeArgs)
    {
        _gameProcess = _latestProcess;

        Process? gameProcess = await (_isExternalInit
            ? WaitGameCSharp(processName, exePath, exeArgs)
            : WaitGameNative(processName, exePath, exeArgs));

        if (gameProcess is not null)
        {
            _gameProcess = gameProcess;
            return _gameProcess;
        }
        return null;
    }

    private unsafe void MonitorLogic(string exePath, string args, StartGameOptions options)
    {
        HANDLE hJob = default;
        HANDLE hIOCP = default;
        PROCESS_INFORMATION pi = default;
        var processCreated = false;

        try
        {
            // 1. CreateJobObject
            hJob = PInvoke.CreateJobObject((SECURITY_ATTRIBUTES*)null, null);
            if (hJob.IsNull) throw new Exception("Failed to create Job");

            // 2. CreateIoCompletionPort
            hIOCP = PInvoke.CreateIoCompletionPort(HANDLE.INVALID_HANDLE_VALUE, HANDLE.Null, 0, 1);
            if (hIOCP.IsNull) throw new Exception("Failed to create IOCP");

            // // 3. Associate Job with IOCP
            var assoc = new JOBOBJECT_ASSOCIATE_COMPLETION_PORT
            {
                CompletionKey = (void*)0, // C# unsafe 指针
                CompletionPort = hIOCP
            };

            if (!PInvoke.SetInformationJobObject(hJob, JOBOBJECTINFOCLASS.JobObjectAssociateCompletionPortInformation, &assoc, (uint)sizeof(JOBOBJECT_ASSOCIATE_COMPLETION_PORT)))
            {
                throw new Exception("Failed to associate Job");
            }

            // 4. CreateProcess (Suspended)
            STARTUPINFOW si = new ()
            {
                cb = (uint)sizeof(STARTUPINFOW)
            };

            // 组合命令行
            var cmdLine = string.IsNullOrWhiteSpace(args) ? $"\"{exePath}\"" : $"\"{exePath}\" {args}";
            var commandLineBuffer = (cmdLine + '\0').ToCharArray();

            string? workDir = options.WorkingDirectory;
            var workDirBuffer = string.IsNullOrEmpty(workDir) ? null : (workDir + '\0').ToCharArray();

            fixed (char* pCmd = commandLineBuffer)
            fixed (char* pWorkDir = workDirBuffer)
            {
                PROCESS_CREATION_FLAGS flags = PROCESS_CREATION_FLAGS.CREATE_SUSPENDED
                                               | PROCESS_CREATION_FLAGS.CREATE_BREAKAWAY_FROM_JOB;
                if (options.CreateNoWindow) flags |= PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW;

                BOOL success = PInvoke.CreateProcess(
                    (PCWSTR)null, // Application Name
                    (PWSTR)pCmd,                 // Command Line
                    (SECURITY_ATTRIBUTES*)null,  // Process Attributes
                    (SECURITY_ATTRIBUTES*)null,  // Thread Attributes
                    (BOOL)false,    // Inherit Handles
                    flags,                       // Creation Flags
                    (void*)null,     // Environment (void*)
                    (PCWSTR)pWorkDir, // Current Directory
                    &si,                         // Startup Info
                    &pi                          // Process Information
                );
                if (!success)
                {
                    // 对于 UAC 管理员权限启动的程序，无法使用任务组，报错并换成 Process 方式启动。
                    throw new Exception($"Launch failed: {Marshal.GetLastWin32Error()}");
                }
                processCreated = true;
            }

            // 5. Assign Process
            if (!PInvoke.AssignProcessToJobObject(hJob, pi.hProcess))
            {
                PInvoke.TerminateProcess(pi.hProcess, 0);
                throw new Exception("Cannot assign process to job");
            }

            // 6. Resume
            if (PInvoke.ResumeThread(pi.hThread) == uint.MaxValue)
            {
                throw new Exception($"Resume failed: {Marshal.GetLastWin32Error()}");
            }

            PInvoke.CloseHandle(pi.hThread);
            PInvoke.CloseHandle(pi.hProcess);

            pi.hThread = HANDLE.Null;
            pi.hProcess = HANDLE.Null;

            // 7. IOCP Loop
            ListenForJobEvents(hIOCP);
        }
        finally
        {
            if (processCreated)
            {
                if (!pi.hThread.IsNull) PInvoke.CloseHandle(pi.hThread);
                if (!pi.hProcess.IsNull) PInvoke.CloseHandle(pi.hProcess);
            }
            if (!hIOCP.IsNull) PInvoke.CloseHandle(hIOCP);
            if (!hJob.IsNull) PInvoke.CloseHandle(hJob);
        }
    }

    private unsafe void ListenForJobEvents(HANDLE hIOCP)
    {
        var sessionActive = true;
        while (sessionActive)
        {
            uint msgId;
            nuint completionKey;
            NativeOverlapped* overlapped;

            if (PInvoke.GetQueuedCompletionStatus(hIOCP, &msgId, &completionKey, &overlapped, PInvoke.INFINITE))
            {
                var pid = (int)overlapped;

                switch (msgId)
                {
                    case PInvoke.JOB_OBJECT_MSG_NEW_PROCESS:
                        _latestProcess = Process.GetProcessById(pid);
                        _runningProcesses[pid] = _latestProcess;
                        OnProcessStarted?.Invoke(pid);
                        break;
                    case PInvoke.JOB_OBJECT_MSG_EXIT_PROCESS:
                        OnProcessExited?.Invoke(pid);
                        if (_runningProcesses.TryRemove(pid, out var process))
                        {
                            // 如果不 dispose 则会泄漏句柄，但是考虑到别的模块可能还在引用 process
                            // 这里暂时先允许句柄泄漏一下。
                            // process.Dispose();
                        }
                        break;
                    case PInvoke.JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO:
                        _runningProcesses.Clear();
                        sessionActive = false;
                        break;
                }
            }
        }
        OnGameSessionEnded?.Invoke();
    }

    private static async Task<Process?> WaitForProcessStartAsync(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        var waitSec = 0;
        while (processes.Length == 0)
        {
            await Task.Delay(100);
            processes = Process.GetProcessesByName(processName);
            if (++waitSec > ProcessMaxWaitSec)
                return null;
        }
        return processes[0];
    }

    private static Process? TryGetProcessFromName(string? exePath)
    {
        if (exePath is null) return null;
        var name = Path.GetFileNameWithoutExtension(exePath);
        return Process.GetProcesses().FirstOrDefault(p => p.ProcessName == name);
    }

}
