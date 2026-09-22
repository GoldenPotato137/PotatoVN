using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Models.Msgs;
using Windows.System;

namespace GalgameManager.Models.BgTasks;

public class KeyMappingTask : BgTaskBase
{
    public string ProcessName { get; set; } = string.Empty;
    public Galgame? Galgame;
    public override bool ProgressOnTrayIcon => false;
    public List<KeyMapping> KeyMappings { get; set; } = new();
    public bool HasPreLaunchProcessSnapshot { get; set; } // 是否已经在启动游戏前采集安装目录进程快照
    public List<int> PreExistingProcessIds { get; set; } = []; // 启动前已经存在于安装目录的进程Id

    private volatile Process? _process;
    private GameRuntimeProcessRelay? _processRelay;
    private string[] _directoryPrefixes = [];
    private readonly HashSet<int> _trackedProcessIds = [];
    private List<KeyMapping> _runtimeGameMappings = [];
    private bool _runtimeGameMappingOptInEnabled;
    private KeyMappingRuntimeSnapshot _snapshot = KeyMappingRuntimeSnapshot.Empty;
    private KeyMappingRuntimeSnapshot? _pendingSnapshot;
    private volatile int _confirmedGameplayProcessId;
    private readonly Dictionary<int, KeyMappingOutput> _activeKeyboardMappings = new();
    private readonly Dictionary<int, KeyMappingOutput> _activeMouseMappings = new();
    private readonly HashSet<int> _pressedPhysicalKeyboardKeys = [];
    private readonly HashSet<int> _pressedPhysicalMouseButtons = [];
    private readonly KeyMappingHeldInputGuard _heldInputGuard = new();
    private readonly KeyMappingOutputState _outputState;
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);
    private ILocalSettingsService? _localSettingsService;
    private IMessenger? _messenger;
    private volatile bool _isMonitoring;
    private nint _keyboardHookId;
    private nint _mouseHookId;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private readonly ManualResetEventSlim _hookStarted = new(false);
    private readonly ManualResetEventSlim _snapshotApplied = new(false);
    private Exception? _hookStartException;
    private int _callbackErrorLogged;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);
    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);
    private LowLevelKeyboardProc? _keyboardHookProc;
    private LowLevelMouseProc? _mouseHookProc;

    #region P_INVOKE

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int nVirtKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, NativeInput[] inputs, int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out NativeMessage lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMessage lpMsg, nint hWnd, uint wMsgFilterMin,
        uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref NativeMessage lpMsg);

    #endregion

    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const uint WmQuit = 0x0012;
    private const uint WmApplySnapshot = 0x8001;
    private const uint LlkhfExtended = 0x01;
    private const uint LlkhfInjected = 0x10;
    private const uint LlmhfInjected = 0x01;

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;
    private const uint MouseEventWheel = 0x0800;

    public KeyMappingTask()
    {
        _outputState = new(SendKeyboardState, SendMouseState);
    }

    public KeyMappingTask(Galgame game, Process process)
        : this(game, process, game.KeyMappings)
    {
    }

    public KeyMappingTask(Galgame game, Process process, IEnumerable<KeyMapping> keyMappings)
        : this(game, process, keyMappings, null, null)
    {
    }

    public KeyMappingTask(Galgame game, Process process, IEnumerable<KeyMapping> keyMappings,
        IReadOnlyCollection<int>? preExistingProcessIds, GameRuntimeProcessRelay? processRelay)
        : this()
    {
        Galgame = game;
        _process = process;
        _processRelay = processRelay;
        HasPreLaunchProcessSnapshot = preExistingProcessIds is not null;
        PreExistingProcessIds = preExistingProcessIds?.ToList() ?? [];
        foreach (int processId in PreExistingProcessIds)
            _trackedProcessIds.Add(processId);
        _trackedProcessIds.Add(GameProcessDetector.SafeGetId(process));
        try
        {
            ProcessName = process.ProcessName;
        }
        catch
        {
            // 短命启动器可能在任务创建前已经退出，仍保留任务以便接力到正式游戏进程。
        }
        KeyMappings = keyMappings.Select(m => new KeyMapping
        {
            From = new List<int>(m.From),
            To = new List<int>(m.To),
            IsEnabled = m.IsEnabled,
            Remark = m.Remark,
            IsGlobal = m.IsGlobal,
        }).ToList();
        _runtimeGameMappings = KeyMappings.Select(KeyMappingMergeHelper.Clone).ToList();
        _runtimeGameMappingOptInEnabled = game.KeyReMap;
        InitDirectoryPrefixes();
    }

    protected override Task RecoverFromJsonInternal()
    {
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        HasPreLaunchProcessSnapshot = false;
        PreExistingProcessIds = [];
        if (_process is not null) _trackedProcessIds.Add(GameProcessDetector.SafeGetId(_process));
        InitDirectoryPrefixes();
        return Task.CompletedTask;
    }

    protected override async Task RunInternal()
    {
        if (_process is null || Galgame is null) return;

        _localSettingsService = App.GetService<ILocalSettingsService>();
        _messenger = App.GetService<IMessenger>();
        _runtimeGameMappings = (Galgame.KeyMappings ?? [])
            .Select(KeyMappingMergeHelper.Clone)
            .ToList();
        _runtimeGameMappingOptInEnabled = Galgame.KeyReMap;
        _isMonitoring = true;
        _localSettingsService.OnSettingChanged += OnSettingChanged;
        Galgame.PropertyChanged += OnGalgamePropertyChanged;
        _messenger.Register<KeyMappingsChangedMessage>(this, (_, message) =>
        {
            if (Galgame?.Uuid == message.GalgameUuid)
                message.Reply(ReloadMappingsSafelyAsync(
                    "game mappings saved",
                    message.GameMappings,
                    message.GameMappingOptInEnabled,
                    reportGameMappingsApplied: true));
        });
        ChangeProgress(0, 1, "KeyMappingTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
        try
        {
            _ = await ReloadMappingsSafelyAsync("game launched");
            await FollowGameProcessAsync();
        }
        finally
        {
            _isMonitoring = false;
            _localSettingsService.OnSettingChanged -= OnSettingChanged;
            Galgame.PropertyChanged -= OnGalgamePropertyChanged;
            _messenger.Unregister<KeyMappingsChangedMessage>(this);
            await _reloadSemaphore.WaitAsync();
            try
            {
                await Task.Run(StopHookThread);
                Volatile.Write(ref _snapshot, KeyMappingRuntimeSnapshot.Empty);
            }
            finally
            {
                _reloadSemaphore.Release();
            }
            ChangeProgress(1, 1, "KeyMappingTask_Done".GetLocalized());
        }
    }

    private void OnSettingChanged(string key, object? value)
    {
        if (key is KeyValues.GameReMapEnabled or KeyValues.GlobalKeyMappings)
            _ = ReloadMappingsSafelyAsync($"setting changed: {key}");
    }

    private void OnGalgamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GalgameManager.Models.Galgame.KeyReMap))
            _ = ReloadMappingsSafelyAsync(
                "game mapping toggle changed",
                updatedGameMappingOptInEnabled: Galgame?.KeyReMap);
    }

    private async Task<bool> ReloadMappingsSafelyAsync(
        string reason,
        IReadOnlyList<KeyMapping>? updatedGameMappings = null,
        bool? updatedGameMappingOptInEnabled = null,
        bool reportGameMappingsApplied = false)
    {
        if (!_isMonitoring || Galgame is null || _localSettingsService is null) return false;

        await _reloadSemaphore.WaitAsync();
        try
        {
            if (!_isMonitoring) return false;
            if (updatedGameMappings is not null)
                _runtimeGameMappings = updatedGameMappings
                    .Select(KeyMappingMergeHelper.Clone)
                    .ToList();
            if (updatedGameMappingOptInEnabled is not null)
                _runtimeGameMappingOptInEnabled = updatedGameMappingOptInEnabled.Value;

            bool globalEnabled =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GameReMapEnabled);
            bool mappingActive = _runtimeGameMappingOptInEnabled || globalEnabled;
            List<KeyMapping> globalMappings = mappingActive
                ? await _localSettingsService.ReadSettingAsync<List<KeyMapping>>(KeyValues.GlobalKeyMappings) ?? []
                : [];
            List<KeyMapping> effectiveMappings = KeyMappingRuntimeSnapshot.BuildEffectiveMappings(
                _runtimeGameMappings,
                globalMappings,
                _runtimeGameMappingOptInEnabled,
                globalEnabled);
            KeyMappings = effectiveMappings.Select(KeyMappingMergeHelper.Clone).ToList();
            KeyMappingRuntimeSnapshot nextSnapshot = KeyMappingRuntimeSnapshot.Create(effectiveMappings);

            await ApplySnapshotAsync(nextSnapshot);
            App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
                $"Key mappings reloaded: gameUuid={Galgame.Uuid:D}, rules={nextSnapshot.LookupMap.Count}, " +
                $"gameOptIn={_runtimeGameMappingOptInEnabled}, globalEnabled={globalEnabled}, " +
                $"mappingActive={mappingActive}, hookActive={!nextSnapshot.IsEmpty}, " +
                $"mappings={DescribeSnapshot(nextSnapshot)}, reason={reason}");
            return !reportGameMappingsApplied || mappingActive;
        }
        catch (Exception exception)
        {
            App.GetService<IInfoService>().DeveloperEvent(
                msg: $"Failed to reload key mappings: gameUuid={Galgame?.Uuid:D}, reason={reason}",
                e: exception);
            return false;
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    private static string DescribeSnapshot(KeyMappingRuntimeSnapshot snapshot)
    {
        if (snapshot.IsEmpty) return "<empty>";
        return string.Join("; ", snapshot.LookupMap
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                KeyMappingOutput output = pair.Value;
                IEnumerable<int> target = output.Modifiers
                    .Concat(output.Key is null ? [] : [output.Key.Value])
                    .Concat(output.MouseButton is null ? [] : [output.MouseButton.Value]);
                return $"{pair.Key}->{string.Join(',', target)}";
            }));
    }

    private async Task ApplySnapshotAsync(KeyMappingRuntimeSnapshot nextSnapshot)
    {
        Thread? hookThread = _hookThread;
        if (hookThread is null || !hookThread.IsAlive || _hookThreadId == 0)
        {
            Volatile.Write(ref _snapshot, nextSnapshot);
            if (!nextSnapshot.IsEmpty)
                await Task.Run(StartHookThread);
            return;
        }

        Interlocked.Exchange(ref _pendingSnapshot, nextSnapshot);
        _snapshotApplied.Reset();
        if (!PostThreadMessage(_hookThreadId, WmApplySnapshot, 0, nint.Zero))
        {
            await RestartHookThreadWithSnapshotAsync(nextSnapshot, hookThread);
            return;
        }

        bool applied = await Task.Run(() => _snapshotApplied.Wait(TimeSpan.FromSeconds(3)));
        if (!applied)
        {
            await RestartHookThreadWithSnapshotAsync(nextSnapshot, hookThread);
            return;
        }
        if (nextSnapshot.IsEmpty)
            await Task.Run(StopHookThread);
    }

    private async Task RestartHookThreadWithSnapshotAsync(
        KeyMappingRuntimeSnapshot nextSnapshot,
        Thread previousHookThread)
    {
        await Task.Run(StopHookThread);
        if (previousHookThread.IsAlive)
            throw new TimeoutException("停止无响应的按键映射钩子线程超时。");

        Interlocked.Exchange(ref _pendingSnapshot, null);
        Volatile.Write(ref _snapshot, nextSnapshot);
        if (!nextSnapshot.IsEmpty)
            await Task.Run(StartHookThread);
    }

    private void InitDirectoryPrefixes()
    {
        _directoryPrefixes = Galgame?.SourceEntries
            .Select(entry => entry.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => GameProcessDetector.GetDirectoryPrefix(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private async Task FollowGameProcessAsync()
    {
        while (_process is not null)
        {
            TryFollowConfirmedProcess();
            Process? tracked = _process;
            if (tracked is null) break;
            while (ReferenceEquals(_process, tracked) && GameProcessDetector.IsAlive(tracked))
            {
                TryFollowConfirmedProcess();
                await Task.Delay(200);
            }
            if (!ReferenceEquals(_process, tracked)) continue;

            int exitedProcessId = GameProcessDetector.SafeGetId(tracked);
            if (exitedProcessId > 0) _trackedProcessIds.Add(exitedProcessId);
            if (!GameSessionExitPolicy.ShouldWaitForReplacement(
                    _confirmedGameplayProcessId > 0, _confirmedGameplayProcessId, exitedProcessId))
                break;
            Process? replacement = await WaitForReplacementProcessAsync();
            if (replacement is null) break;

            AttachProcess(replacement);
        }
    }

    private void TryFollowConfirmedProcess()
    {
        Process? confirmed = _processRelay?.ConfirmedProcess;
        if (confirmed is null || !GameProcessDetector.IsAlive(confirmed)) return;
        int confirmedProcessId = GameProcessDetector.SafeGetId(confirmed);
        if (confirmedProcessId <= 0) return;

        _confirmedGameplayProcessId = confirmedProcessId;
        if (_process is not null && GameProcessDetector.SafeGetId(_process) == confirmedProcessId) return;
        AttachProcess(confirmed);
    }

    private void AttachProcess(Process process)
    {
        _process = process;
        _trackedProcessIds.Add(GameProcessDetector.SafeGetId(process));
        try
        {
            ProcessName = process.ProcessName;
        }
        catch
        {
            // 进程可能在身份切换期间退出，后续仍由生命周期检查完成清理。
        }
    }

    private async Task<Process?> WaitForReplacementProcessAsync()
    {
        if (_directoryPrefixes.Length == 0) return null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            foreach (string prefix in _directoryPrefixes)
            {
                Process? candidate = GameProcessDetector.FindBestProcessInDirectory(prefix, _trackedProcessIds);
                if (candidate is not null) return candidate;
            }
            await Task.Delay(1000);
        }
        return null;
    }

    private void StartHookThread()
    {
        _hookStartException = null;
        _hookStarted.Reset();
        _hookThread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "PotatoVN native key mapping hook",
        };
        _hookThread.Start();

        if (!_hookStarted.Wait(TimeSpan.FromSeconds(5)))
        {
            StopHookThread();
            throw new TimeoutException("启动键位映射钩子超时。");
        }
        if (_hookStartException is not null)
        {
            Exception inner = _hookStartException;
            StopHookThread();
            throw new InvalidOperationException("无法启动键位映射钩子。", inner);
        }
    }

    private void HookThreadMain()
    {
        try
        {
            _hookThreadId = GetCurrentThreadId();
            _ = PeekMessage(out _, nint.Zero, 0, 0, 0);

            _keyboardHookProc = KeyboardHookCallback;
            _mouseHookProc = MouseHookCallback;
            nint moduleHandle = GetModuleHandle(null);

            _keyboardHookId = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, moduleHandle, 0);
            if (_keyboardHookId == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            _mouseHookId = SetWindowsHookEx(WhMouseLl, _mouseHookProc, moduleHandle, 0);
            if (_mouseHookId == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            SeedPressedPhysicalKeys();
            SeedPressedPhysicalMouseButtons();
            _heldInputGuard.BeginTransition(_pressedPhysicalKeyboardKeys, _pressedPhysicalMouseButtons);
            _hookStarted.Set();
            while (true)
            {
                int result = GetMessage(out NativeMessage message, nint.Zero, 0, 0);
                if (result == 0) break;
                if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (message.Message == WmApplySnapshot)
                {
                    ApplyPendingSnapshot();
                    continue;
                }
                _ = TranslateMessage(ref message);
                _ = DispatchMessage(ref message);
            }
        }
        catch (Exception ex)
        {
            _hookStartException ??= ex;
            _hookStarted.Set();
        }
        finally
        {
            if (_keyboardHookId != nint.Zero)
            {
                _ = UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = nint.Zero;
            }
            if (_mouseHookId != nint.Zero)
            {
                _ = UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = nint.Zero;
            }

            ReleaseAllOutputs();
            _keyboardHookProc = null;
            _mouseHookProc = null;
            _hookThreadId = 0;
        }
    }

    private void StopHookThread()
    {
        Thread? thread = _hookThread;
        if (thread is null) return;

        uint threadId = _hookThreadId;
        if (threadId != 0) _ = PostThreadMessage(threadId, WmQuit, 0, nint.Zero);
        if (Thread.CurrentThread != thread) _ = thread.Join(TimeSpan.FromSeconds(3));
        if (!thread.IsAlive) _hookThread = null;
    }

    private void ApplyPendingSnapshot()
    {
        KeyMappingRuntimeSnapshot? nextSnapshot = Interlocked.Exchange(ref _pendingSnapshot, null);
        if (nextSnapshot is null)
        {
            _snapshotApplied.Set();
            return;
        }

        ReleaseMappedOutputs();
        _heldInputGuard.BeginTransition(_pressedPhysicalKeyboardKeys, _pressedPhysicalMouseButtons);
        Volatile.Write(ref _snapshot, nextSnapshot);
        _snapshotApplied.Set();
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0) return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

        try
        {
            KbdLlHookStruct data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if ((data.Flags & LlkhfInjected) != 0)
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

            int message = unchecked((int)wParam.ToInt64());
            int sourceKey = (int)ResolvePhysicalModifier(data);
            if (message is WmKeyUp or WmSysKeyUp)
            {
                _pressedPhysicalKeyboardKeys.Remove(sourceKey);
                _heldInputGuard.ReleaseKeyboard(sourceKey);
                if (_activeKeyboardMappings.Remove(sourceKey, out KeyMappingOutput? active))
                {
                    ReleaseOutput(active);
                    return 1;
                }
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
            }
            if (message is not (WmKeyDown or WmSysKeyDown))
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

            _pressedPhysicalKeyboardKeys.Add(sourceKey);
            if (_activeKeyboardMappings.ContainsKey(sourceKey)) return 1;
            List<int> sourceKeys = BuildPressedKeyboardSourceKeys(sourceKey);
            if (_heldInputGuard.IsKeyboardSuppressed(sourceKeys))
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
            string keyString = KeyMappingMergeHelper.CreateSourceSignature(sourceKeys);
            KeyMappingRuntimeSnapshot snapshot = Volatile.Read(ref _snapshot);
            snapshot.LookupMap.TryGetValue(keyString, out KeyMappingOutput? output);

            bool targetForeground = IsTargetGameForeground(out _, out _);
            if (!targetForeground || output is null)
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

            PressOutput(output);
            _activeKeyboardMappings[sourceKey] = output;
            return 1;
        }
        catch (Exception ex)
        {
            LogCallbackError(ex);
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0) return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

        try
        {
            MsllHookStruct data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            if ((data.Flags & LlmhfInjected) != 0)
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

            int message = unchecked((int)wParam.ToInt64());
            int mouseButton = GetMouseButton(message, data.MouseData);
            if (mouseButton == 0) return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

            if (IsMouseButtonUp(message))
            {
                _pressedPhysicalMouseButtons.Remove(mouseButton);
                _heldInputGuard.ReleaseMouse(mouseButton);
                if (_activeMouseMappings.Remove(mouseButton, out KeyMappingOutput? active))
                {
                    ReleaseOutput(active);
                    return 1;
                }
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            if (message != WmMouseWheel)
                _pressedPhysicalMouseButtons.Add(mouseButton);

            KeyMappingRuntimeSnapshot snapshot = Volatile.Read(ref _snapshot);
            List<int> sourceKeyboardKeys = GetPressedMouseSourceKeyboardKeys(snapshot, mouseButton);
            if (_heldInputGuard.IsMouseSuppressed(mouseButton, sourceKeyboardKeys))
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            bool targetForeground = IsTargetGameForeground(out _, out _);
            string sourceSignature = KeyMappingMergeHelper.CreateSourceSignature(
                sourceKeyboardKeys.Prepend(mouseButton));
            snapshot.LookupMap.TryGetValue(sourceSignature, out KeyMappingOutput? output);
            if (!targetForeground || output is null)
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

            if (message == WmMouseWheel)
            {
                PulseOutput(output);
                return 1;
            }
            if (_activeMouseMappings.ContainsKey(mouseButton)) return 1;

            PressOutput(output);
            _activeMouseMappings[mouseButton] = output;
            return 1;
        }
        catch (Exception ex)
        {
            LogCallbackError(ex);
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }
    }

    private bool IsTargetGameForeground(out uint foregroundProcessId, out string matchReason)
    {
        nint window = GetForegroundWindow();
        foregroundProcessId = 0;
        if (window == nint.Zero)
        {
            matchReason = "无前台窗口";
            return false;
        }
        _ = GetWindowThreadProcessId(window, out foregroundProcessId);
        if (foregroundProcessId == 0)
        {
            matchReason = "前台进程未知";
            return false;
        }

        try
        {
            Process? tracked = _process;
            if (tracked is not null && GameProcessDetector.IsAlive(tracked) &&
                GameProcessDetector.SafeGetId(tracked) == foregroundProcessId)
            {
                matchReason = "跟踪进程一致";
                return true;
            }
        }
        catch
        {
            // 继续通过安装目录判断。
        }

        try
        {
            using Process foreground = Process.GetProcessById(checked((int)foregroundProcessId));
            string? executablePath = foreground.TryGetExecutablePath();
            if (executablePath is not null && _directoryPrefixes.Any(prefix =>
                    executablePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                matchReason = "位于游戏安装目录";
                return true;
            }

            if (_directoryPrefixes.Length == 0 &&
                foreground.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                matchReason = "进程名一致";
                return true;
            }

            matchReason = executablePath is null
                ? $"前台进程={foreground.ProcessName}，路径不可读"
                : $"前台进程={foreground.ProcessName}";
            return false;
        }
        catch
        {
            matchReason = "读取前台进程失败";
            return false;
        }
    }

    private List<int> BuildPressedKeyboardSourceKeys(int currentKey)
    {
        int normalizedCurrent = (int)NormalizeExactModifier((VirtualKey)currentKey);
        List<int> pressed = [normalizedCurrent];
        AddPressedModifiers(pressed);
        return pressed;
    }

    private List<int> GetPressedMouseSourceKeyboardKeys(KeyMappingRuntimeSnapshot snapshot, int mouseButton)
    {
        List<int> pressed = [];
        if (snapshot.MouseSourceKeyboardKeysByButton.TryGetValue(mouseButton, out var candidates))
            pressed.AddRange(candidates.Where(IsMouseSourceCandidateDown));
        return pressed;
    }

    private bool IsMouseSourceCandidateDown(int key) =>
        (VirtualKey)key == VirtualKey.LeftWindows
            ? _pressedPhysicalKeyboardKeys.Contains((int)VirtualKey.LeftWindows) ||
              _pressedPhysicalKeyboardKeys.Contains((int)VirtualKey.RightWindows)
            : _pressedPhysicalKeyboardKeys.Contains(key);

    private void SeedPressedPhysicalKeys()
    {
        KeyMappingRuntimeSnapshot snapshot = Volatile.Read(ref _snapshot);
        HashSet<int> candidates =
        [
            (int)VirtualKey.LeftControl,
            (int)VirtualKey.RightControl,
            (int)VirtualKey.LeftShift,
            (int)VirtualKey.RightShift,
            (int)VirtualKey.LeftMenu,
            (int)VirtualKey.RightMenu,
            (int)VirtualKey.LeftWindows,
            (int)VirtualKey.RightWindows,
        ];
        candidates.UnionWith(snapshot.MouseSourceKeyboardKeysByButton.Values.SelectMany(keys => keys));
        foreach (int key in candidates.Where(IsKeyDown))
            _pressedPhysicalKeyboardKeys.Add(key);
    }

    private void SeedPressedPhysicalMouseButtons()
    {
        for (var button = 1; button <= 5; button++)
        {
            int virtualKey = KeyMappingMergeHelper.GetMouseButtonVirtualKey(button);
            if (virtualKey != 0 && IsKeyDown(virtualKey))
                _pressedPhysicalMouseButtons.Add(button);
        }
    }

    private void AddPressedModifiers(ICollection<int> pressed)
    {
        void AddIfPressed(VirtualKey key)
        {
            if (_pressedPhysicalKeyboardKeys.Contains((int)key)) pressed.Add((int)key);
        }

        AddIfPressed(VirtualKey.LeftControl);
        AddIfPressed(VirtualKey.RightControl);
        AddIfPressed(VirtualKey.LeftShift);
        AddIfPressed(VirtualKey.RightShift);
        AddIfPressed(VirtualKey.LeftMenu);
        AddIfPressed(VirtualKey.RightMenu);
        if (_pressedPhysicalKeyboardKeys.Contains((int)VirtualKey.LeftWindows) ||
            _pressedPhysicalKeyboardKeys.Contains((int)VirtualKey.RightWindows))
            pressed.Add((int)VirtualKey.LeftWindows);
    }

    private void PressOutput(KeyMappingOutput output) => _outputState.Press(output);

    private void ReleaseOutput(KeyMappingOutput output) => _outputState.Release(output);

    private void PulseOutput(KeyMappingOutput output) => _outputState.Pulse(output);

    private void ReleaseAllOutputs()
    {
        ReleaseMappedOutputs();
        _pressedPhysicalKeyboardKeys.Clear();
        _pressedPhysicalMouseButtons.Clear();
        _heldInputGuard.Clear();
    }

    private void ReleaseMappedOutputs()
    {
        try
        {
            _outputState.Reset();
        }
        catch
        {
            // 退出时尽力释放，不能阻止钩子线程结束。
        }
        _activeKeyboardMappings.Clear();
        _activeMouseMappings.Clear();
    }

    private static void SendMouseState(int mouseButton, bool down)
    {
        (uint flags, uint mouseData) = mouseButton switch
        {
            1 => (down ? MouseEventLeftDown : MouseEventLeftUp, 0u),
            2 => (down ? MouseEventRightDown : MouseEventRightUp, 0u),
            3 => (down ? MouseEventMiddleDown : MouseEventMiddleUp, 0u),
            4 => (down ? MouseEventXDown : MouseEventXUp, 1u),
            5 => (down ? MouseEventXDown : MouseEventXUp, 2u),
            6 when down => (MouseEventWheel, 120u),
            7 when down => (MouseEventWheel, unchecked((uint)-120)),
            _ => (0u, 0u),
        };
        if (flags == 0) return;

        SendNativeInput(new NativeInput
        {
            Type = InputMouse,
            Data = new NativeInputUnion
            {
                Mouse = new NativeMouseInput
                {
                    MouseData = mouseData,
                    Flags = flags,
                },
            },
        });
    }

    private static void SendKeyboardState(int virtualKey, bool down)
    {
        uint flags = down ? 0 : KeyEventKeyUp;
        if (IsExtendedKey(virtualKey)) flags |= KeyEventExtendedKey;

        SendNativeInput(new NativeInput
        {
            Type = InputKeyboard,
            Data = new NativeInputUnion
            {
                Keyboard = new NativeKeyboardInput
                {
                    VirtualKey = checked((ushort)virtualKey),
                    Flags = flags,
                },
            },
        });
    }

    private static void SendNativeInput(NativeInput input)
    {
        NativeInput[] inputs = [input];
        uint sent = SendInput(1, inputs, Marshal.SizeOf<NativeInput>());
        if (sent != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput 未能发送按键映射输入。");
    }

    private static bool IsExtendedKey(int virtualKey) => virtualKey is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or
        0x2D or 0x2E or 0x5B or 0x5C or 0x6F or 0x90 or 0xA3 or 0xA5;

    private void LogCallbackError(Exception exception)
    {
        if (Interlocked.Exchange(ref _callbackErrorLogged, 1) != 0) return;
        App.GetService<IInfoService>().DeveloperEvent(
            msg: "键位映射处理输入事件时发生错误。",
            e: exception);
    }

    private static bool IsMouseButtonCode(int code) => code is >= 1 and <= 7;

    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool IsMouseButtonUp(int message) =>
        message is WmLButtonUp or WmRButtonUp or WmMButtonUp or WmXButtonUp;

    private static int GetMouseButton(int message, uint mouseData) => message switch
    {
        WmLButtonDown or WmLButtonUp => 1,
        WmRButtonDown or WmRButtonUp => 2,
        WmMButtonDown or WmMButtonUp => 3,
        WmXButtonDown or WmXButtonUp => (ushort)(mouseData >> 16) == 1 ? 4 : 5,
        WmMouseWheel => unchecked((short)(mouseData >> 16)) > 0 ? 6 : 7,
        _ => 0,
    };

    private static VirtualKey NormalizeExactModifier(VirtualKey key) => key switch
    {
        VirtualKey.RightWindows => VirtualKey.LeftWindows,
        _ => key,
    };

    private static VirtualKey ResolvePhysicalModifier(KbdLlHookStruct data)
    {
        VirtualKey key = (VirtualKey)data.VkCode;
        return key switch
        {
            VirtualKey.Shift => data.ScanCode == 0x36
                ? VirtualKey.RightShift
                : VirtualKey.LeftShift,
            VirtualKey.Control => (data.Flags & LlkhfExtended) != 0
                ? VirtualKey.RightControl
                : VirtualKey.LeftControl,
            VirtualKey.Menu => (data.Flags & LlkhfExtended) != 0
                ? VirtualKey.RightMenu
                : VirtualKey.LeftMenu,
            _ => key,
        };
    }

    private static string GetKeyDisplayName(VirtualKey key) => key switch
    {
        VirtualKey.LeftWindows or VirtualKey.RightWindows => "Win",
        VirtualKey.Control => "Ctrl",
        VirtualKey.LeftControl => "左 Ctrl",
        VirtualKey.RightControl => "右 Ctrl",
        VirtualKey.Menu => "Alt",
        VirtualKey.LeftMenu => "左 Alt",
        VirtualKey.RightMenu => "右 Alt",
        VirtualKey.Shift => "Shift",
        VirtualKey.LeftShift => "左 Shift",
        VirtualKey.RightShift => "右 Shift",
        _ => key.ToString(),
    };


    public override string Title { get; } = "KeyMappingTask_Title".GetLocalized();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public NativeInputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)] public NativeMouseInput Mouse;
        [FieldOffset(0)] public NativeKeyboardInput Keyboard;
        [FieldOffset(0)] public NativeHardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}
