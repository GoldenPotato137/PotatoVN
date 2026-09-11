using System.Diagnostics;
using System.Runtime.InteropServices;
using GalgameManager.Helpers;

namespace GalgameManager.Models.BgTasks;

public class GameMuteTask : BgTaskBase
{
    public Galgame? Galgame { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsMuted { get; set; }
    private Process? _process;
    private GameRuntimeProcessRelay? _processRelay;

    public GameMuteTask() { } // 仅用于后台任务序列化恢复

    public GameMuteTask(Galgame game, Process process)
        : this(game, process, null)
    {
    }

    public GameMuteTask(Galgame game, Process process, GameRuntimeProcessRelay? processRelay)
    {
        Galgame = game;
        _process = process;
        _processRelay = processRelay;
        UpdateProcessIdentity(process);
    }

    protected override Task RecoverFromJsonInternal()
    {
        try
        {
            _process = Process.GetProcessById(ProcessId);
        }
        catch
        {
            // 进程可能已经退出，回退到按名称查找。
            _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        }
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        if (Galgame is null || _process is null) return;
        ChangeProgress(0, 1, "GameMuteTask_Starting".GetLocalized(Galgame.Name.Value!));
        
        // 确保开始时取消静音，防止上次异常退出导致的残留
        if (ProcessId > 0) AudioHelper.UnmuteProcess(ProcessId);

        while (true)
        {
            TryFollowConfirmedProcess();
            Process? current = _process;
            if (current is null || !GameProcessDetector.IsAlive(current))
            {
                if (_processRelay is null || _processRelay.IsCompleted) break;
                await Task.Delay(200);
                continue;
            }

            try
            {
                bool shouldMute = !current.IsMainWindowFocused();
                if (shouldMute)
                {
                    // 需要静音
                    if (!IsMuted)
                    {
                        if (AudioHelper.MuteProcess(current.Id))
                        {
                            IsMuted = true;
                            ChangeProgress(0, 1, "GameMuteTask_Muted".GetLocalized(Galgame.Name.Value!));
                        }
                    }
                }
                else 
                {
                    // 需要有声音 (在前台)
                    // 检查 IsMuted 标记或者系统实际状态
                    if (IsMuted || AudioHelper.IsProcessMuted(current.Id))
                    {
                        if (AudioHelper.UnmuteProcess(current.Id))
                        {
                            IsMuted = false;
                            ChangeProgress(0, 1, "GameMuteTask_Unmuted".GetLocalized(Galgame.Name.Value!));
                        }
                    }
                }

                // 每秒检查一次
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                // 记录错误但继续运行
                ChangeProgress(0, 1, $"GameMuteTask_MonitorError".GetLocalized() + ": " + ex.Message);
                await Task.Delay(5000); // 出错时等待更长时间
            }
        }
        
        // 结束时尝试取消静音
        try
        {
            if (ProcessId > 0) AudioHelper.UnmuteProcess(ProcessId);
        }
        catch
        {
            // 进程可能已退出，结束清理无需继续抛出异常。
        }
        ChangeProgress(1, 1, string.Empty, false);
    }

    private void TryFollowConfirmedProcess()
    {
        Process? confirmed = _processRelay?.ConfirmedProcess;
        if (confirmed is null || !GameProcessDetector.IsAlive(confirmed)) return;
        int confirmedProcessId = GameProcessDetector.SafeGetId(confirmed);
        if (confirmedProcessId <= 0 || confirmedProcessId == ProcessId) return;

        try
        {
            if (ProcessId > 0) AudioHelper.UnmuteProcess(ProcessId);
        }
        catch
        {
            // 原启动器可能已经退出，切换进程时无需保留其静音状态。
        }

        _process = confirmed;
        IsMuted = false;
        UpdateProcessIdentity(confirmed);
        AudioHelper.UnmuteProcess(ProcessId);
    }

    private void UpdateProcessIdentity(Process process)
    {
        ProcessId = GameProcessDetector.SafeGetId(process);
        try
        {
            ProcessName = process.ProcessName;
        }
        catch
        {
            // 短命启动器可能在任务创建前退出，稍后仍可接力到正式游戏进程。
        }
    }

    public override string Title => "GameMuteTask_Title".GetLocalized();
}

public static class AudioHelper
{
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("ole32.dll")]
    private static extern int CoInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    // Windows Core Audio API 接口
    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig]
        int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice ppDevice);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        int NotImpl1();
        int NotImpl2();
        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int SessionCount);
        [PreserveSig]
        int GetSession(int SessionCount, out IAudioSessionControl2 Session);
    }

    [ComImport]
    [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig]
        int GetState(out AudioSessionState pRetVal);
        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);
        [PreserveSig]
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);
        [PreserveSig]
        int GetGroupingParam(out Guid pRetVal);
        [PreserveSig]
        int SetGroupingParam([MarshalAs(UnmanagedType.LPStruct)] Guid Override, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);
        [PreserveSig]
        int RegisterAudioSessionNotification(IAudioSessionEvents NewNotifications);
        [PreserveSig]
        int UnregisterAudioSessionNotification(IAudioSessionEvents NewNotifications);
        [PreserveSig]
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig]
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig]
        int GetProcessId(out uint pRetVal);
        [PreserveSig]
        int IsSystemSoundsSession();
        [PreserveSig]
        int SetDuckingPreference(bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig]
        int GetMasterVolume(out float pfLevel);
        [PreserveSig]
        int SetMute(bool bMute, ref Guid EventContext);
        [PreserveSig]
        int GetMute(out bool pbMute);
    }

    private interface IAudioSessionEvents
    {
    }

    private enum DataFlow
    {
        Render,
        Capture,
        All
    }

    private enum Role
    {
        Console,
        Multimedia,
        Communications
    }

    private enum AudioSessionState
    {
        Inactive = 0,
        Active = 1,
        Expired = 2
    }

    public static bool IsProcessMuted(int processId)
    {
        return RunOnAudioSession(processId, volume =>
        {
            volume.GetMute(out bool isMuted);
            return isMuted;
        });
    }

    public static bool MuteProcess(int processId)
    {
        return SetProcessMute(processId, true);
    }

    public static bool UnmuteProcess(int processId)
    {
        return SetProcessMute(processId, false);
    }

    private static bool SetProcessMute(int processId, bool mute)
    {
        return RunOnAudioSession(processId, volume =>
        {
            var eventContext = Guid.Empty;
            volume.SetMute(mute, ref eventContext);
            return true;
        });
    }

    private static bool RunOnAudioSession(int processId, Func<ISimpleAudioVolume, bool> action)
    {
        try
        {
            CoInitialize(IntPtr.Zero);

            var deviceEnumerator = new MMDeviceEnumerator() as IMMDeviceEnumerator;
            if (deviceEnumerator == null) return false;

            deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out IMMDevice device);
            if (device == null) return false;

            var iidIAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
            device.Activate(ref iidIAudioSessionManager2, 0, IntPtr.Zero, out object o);
            var mgr = o as IAudioSessionManager2;
            if (mgr == null) return false;

            mgr.GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
            if (sessionEnumerator == null) return false;

            sessionEnumerator.GetCount(out int count);

            for (int i = 0; i < count; i++)
            {
                sessionEnumerator.GetSession(i, out IAudioSessionControl2 ctl);
                if (ctl == null) continue;

                ctl.GetProcessId(out uint sessionProcessId);
                if (sessionProcessId == processId)
                {
                    var simpleVolume = ctl as ISimpleAudioVolume;
                    if (simpleVolume != null)
                    {
                        return action(simpleVolume);
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            CoUninitialize();
        }
    }
}
