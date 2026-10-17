using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.ViewModels;
using GalgameManager.WinApp.Base.Models.Msgs;

namespace GalgameManager.Models.BgTasks;

public class RecordPlayTimeTask : BgTaskBase
{
    private const int ManuallySelectProcessSec = 15; // 认定为需要手动选择游戏进程的时间阈值

    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public DateTime? ProcessStartTime { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public int CurrentPlayTime { get; set; } // 本次游玩时间
    public long CurrentPlayTimeSeconds { get; set; } // 本次实际累计秒数
    public bool? PrecisePlayTimeMode { get; set; } // 本次启动锁定的计时模式，恢复任务时保持不变
    public Guid? ActiveSessionId { get; set; } // 异常退出/托盘恢复时继续同一原生时段
    public Guid? ActiveMinuteSessionId { get; set; } // 异常退出/托盘恢复时继续同一条分钟级启动分段
    public Guid? InstallationId { get; set; }
    public bool HasPreLaunchProcessSnapshot { get; set; }
    public List<int> PreExistingProcessIds { get; set; } = [];
    public bool DelayPlayTimeUntilMainWindow { get; set; }
    public bool? RecordingStarted { get; set; } // 空值兼容没有保存计时阶段的旧任务
    public GameWindowSnapshot? LaunchWindowBaseline
    {
        get => _launchWindowTracker?.Baseline ?? _launchWindowBaseline;
        set => _launchWindowBaseline = value;
    }
    public override bool ProgressOnTrayIcon => true;

    public Galgame? Galgame;
    private volatile Process? _process;
    private string? _directoryPrefix;
    private HashSet<int> _knownProcessIds = [];
    private readonly HashSet<Process> _attachedProcesses = [];
    private volatile bool _stopped;
    private bool _recoveredFromJson;
    private bool _recordingStartedMessageSent;
    private int _confirmedGameplayProcessId;
    private readonly StableProcessHandoffGate _foregroundProcessHandoffGate = new();
    private readonly StableGameWindowGate _directWindowGate = new();
    private GameRuntimeProcessRelay _processRelay = new();
    private GameLaunchWindowTracker? _launchWindowTracker;
    private GameWindowSnapshot? _launchWindowBaseline;
    private readonly TaskCompletionSource _recordingReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal GameRuntimeProcessRelay ProcessRelay => _processRelay;

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IGalgameCollectionService _gameService;
    private int _minPlayTimeRecordThreshold;

    public RecordPlayTimeTask()
        : this(App.GetService<ILocalSettingsService>(), App.GetService<IGalgameCollectionService>())
    {
    }

    /// <summary>
    /// 使用明确的服务依赖创建计时任务。
    /// </summary>
    public RecordPlayTimeTask(ILocalSettingsService localSettingsService, IGalgameCollectionService gameService)
    {
        _localSettingsService = localSettingsService;
        _gameService = gameService;
    }

    public RecordPlayTimeTask(Galgame game, Process process)
        : this(game, process, game.PreferredInstallationId)
    {
    }

    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId)
        : this(game, process, installationId, null, null)
    {
    }

    /// <summary>
    /// 使用启动前快照和可选弹窗追踪器创建计时任务；任务独占进程接力判断。
    /// </summary>
    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId,
        IReadOnlyCollection<int>? preExistingProcessIds, GameRuntimeProcessRelay? processRelay,
        GameLaunchWindowTracker? launchWindowTracker = null)
        : this()
    {
        Debug.Assert(game.IsLocalGame);
        Galgame = game;
        InstallationId = installationId;
        HasPreLaunchProcessSnapshot = preExistingProcessIds is not null;
        PreExistingProcessIds = preExistingProcessIds?.ToList() ?? [];
        _processRelay = processRelay ?? new GameRuntimeProcessRelay();
        _launchWindowTracker = launchWindowTracker;
        DelayPlayTimeUntilMainWindow = game.SourceEntries.FirstOrDefault(e => e.EntryId == installationId)
            ?.LocalConfig?.DelayPlayTimeUntilMainWindow == true;
        RecordingStarted = false;
        InitDirectoryWatch();
        TrackProcess(process);
    }

    protected override Task RecoverFromJsonInternal()
    {
        _recoveredFromJson = true;
        InitDirectoryWatch();
        // 旧任务无法还原零分钟时是否已进入游戏，保守地继续弹窗等待，不补算离线时间。
        RecordingStarted ??= !DelayPlayTimeUntilMainWindow || CurrentPlayTime > 0 ||
                             CurrentPlayTimeSeconds > 0 || ActiveSessionId.HasValue || ActiveMinuteSessionId.HasValue;
        _process = FindRestoredProcess();
        if (_process is not null) TrackProcess(_process);
        return Task.CompletedTask;
    }

    private Process? FindRestoredProcess()
    {
        if (ProcessId > 0)
        {
            Process? candidate = null;
            try
            {
                candidate = Process.GetProcessById(ProcessId);
                if (ProcessStartTime.HasValue && candidate.StartTime != ProcessStartTime.Value)
                {
                    candidate.Dispose();
                    return null;
                }
                if (GameProcessDetector.IsAlive(candidate) &&
                    (_directoryPrefix is null || GameProcessDetector.IsProcessInDirectory(candidate, _directoryPrefix)))
                    return candidate;
            }
            catch
            {
                // 原 PID 已退出、被复用或拒绝读取时不能直接附着到另一轮启动。
            }
            candidate?.Dispose();
            if (RecordingStarted == true) return null;
        }
        return _directoryPrefix is null ? null : GameProcessDetector.FindBestProcessInDirectory(
            _directoryPrefix, PreExistingProcessIds, RecordingStarted == false ? null : ProcessName);
    }

    private void InitDirectoryWatch()
    {
        string? path = Galgame?.SourceEntries.FirstOrDefault(e => e.EntryId == InstallationId)?.Path;
        if (string.IsNullOrEmpty(path)) return;
        _directoryPrefix = GameProcessDetector.GetDirectoryPrefix(path);
        _knownProcessIds = HasPreLaunchProcessSnapshot
            ? new HashSet<int>(PreExistingProcessIds)
            : GameProcessDetector.GetProcessIdsInDirectory(_directoryPrefix);
    }

    protected override async Task RunInternal()
    {
        try
        {
            await RunCoreAsync();
        }
        finally
        {
            _processRelay.Complete();
            try
            {
                if (_launchWindowTracker is not null) await _launchWindowTracker.DisposeAsync();
            }
            finally
            {
                foreach (Process process in _attachedProcesses) process.Dispose();
            }
        }
    }

    private async Task RunCoreAsync()
    {
        if (Galgame is null) return;
        if (_process is null)
        {
            if (_recoveredFromJson) await FinalizeOrphanedRecoveredSessionsAsync();
            return;
        }
        PrecisePlayTimeMode = PlayTimeRecordingModeHelper.ResolvePreciseMode(
            PrecisePlayTimeMode, ActiveSessionId.HasValue,
            await _localSettingsService.ReadSettingAsync<bool>(KeyValues.PrecisePlayTime));
        _minPlayTimeRecordThreshold = await _localSettingsService.ReadSettingAsync<int>(KeyValues.MinPlayTimeRecordThreshold);
        if (!DelayPlayTimeUntilMainWindow || RecordingStarted == true)
            StartRecording();
        else
        {
            RecordingStarted = false;
            _launchWindowTracker ??= new GameLaunchWindowTracker(LaunchWindowBaseline);
            _launchWindowTracker.TrackProcess(ProcessId);
            _launchWindowTracker.Start(_directoryPrefix, PreExistingProcessIds);
            ChangeProgress(0, 1, "RecordPlayTimeTask_WaitingForMainWindow".GetLocalized(Galgame.Name.Value!));
        }

        using CancellationTokenSource recordingCancellation = new();
        Task recording = RecordPlayTimeAsync(recordingCancellation.Token);
        try
        {
            await MonitorProcessAsync(recording);
        }
        finally
        {
            _stopped = true;
            await recordingCancellation.CancelAsync();
            try
            {
                await recording;
            }
            catch (OperationCanceledException) when (recordingCancellation.IsCancellationRequested)
            {
                // 结束后立即释放采样等待和设置监听，不留下后台采样任务。
            }
        }
        await FinishRecordingAsync();
    }

    private async Task MonitorProcessAsync(Task recording)
    {
        while (_process is { } tracked)
        {
            if (recording.IsFaulted) await recording;
            if (GameProcessDetector.IsAlive(tracked))
            {
                // 窗口已经稳定不意味着启动器永远不会再接力，仍观察本次启动的新前台进程。
                TryAttachStableForegroundProcess();
                TryConfirmGameplayWindow();
                await Task.WhenAny(recording, Task.Delay(RecordingStarted == true ? 500 : 100));
                continue;
            }
            if (!GameSessionExitPolicy.ShouldWaitForReplacement(
                    RecordingStarted == true, _confirmedGameplayProcessId, GameProcessDetector.SafeGetId(tracked),
                    _foregroundProcessHandoffGate.HasPendingCandidate))
                break;
            Process? replacement = await WaitForReplacementProcessAsync(GameProcessDetector.SafeGetId(tracked));
            if (replacement is null) break;
            TrackProcess(replacement);
            Log($"Process attached after exit: pid={ProcessId}, process={ProcessName}");
        }
    }

    private async Task FinishRecordingAsync()
    {
        bool precisePlayTime = PrecisePlayTimeMode == true;
        var windowMode = await _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode);
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            GalgamePageParameter parameter = new()
            {
                Galgame = Galgame!,
                SelectProgress = DateTime.Now - StartTime < TimeSpan.FromSeconds(ManuallySelectProcessSec)
                                 && Galgame!.SourceEntries.FirstOrDefault(e => e.EntryId == InstallationId)
                                     ?.LocalConfig?.ProcessName is null,
            };
            if (windowMode == WindowMode.SystemTray)
                App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, parameter);
            App.SetWindowMode(WindowMode.Normal);
            ChangeProgress(1, 1, "RecordPlayTimeTask_Done".GetLocalized(Galgame!.Name.Value ?? string.Empty,
                precisePlayTime
                    ? TimeToDisplayTimeConverter.ConvertSeconds(CurrentPlayTimeSeconds)
                    : TimeToDisplayTimeConverter.Convert(CurrentPlayTime)));
            Galgame.RaisePropertyChanged(nameof(Galgame.LastPlayTime));
            bool reachesPlayCountThreshold = precisePlayTime
                ? CurrentPlayTimeSeconds > 0 && CurrentPlayTimeSeconds >= _minPlayTimeRecordThreshold * 60L
                : CurrentPlayTime >= _minPlayTimeRecordThreshold;
            if (reachesPlayCountThreshold) Galgame.PlayCount++;
            App.GetService<IMessenger>().Send(new GalgameStoppedMessage(Galgame));
        });
        await _gameService.SaveGalgameAsync(Galgame!);
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGames))
            App.GetService<IPvnService>().Upload(Galgame!, PvnUploadProperties.PlayTime);
    }

    /// <summary>
    /// 恢复时若原进程已经退出，使用最后一次持久化的边界关闭开放记录，
    /// 避免详情页永久显示“正在记录”。这里不会把应用离线后的时间补入汇总。
    /// </summary>
    private async Task FinalizeOrphanedRecoveredSessionsAsync()
    {
        bool changed = false;
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            if (ActiveSessionId is { } preciseSessionId)
            {
                changed |= PlayTimeSessionHelper.CloseOpenSession(Galgame!, preciseSessionId);
                ActiveSessionId = null;
            }

            if (ActiveMinuteSessionId is { } minuteSessionId)
            {
                PlayTimeSession? minuteSession = Galgame!.PlayTimeSessions.FirstOrDefault(session =>
                    session.Id == minuteSessionId && session.Kind == PlayTimeSessionKind.MinuteSampled);
                changed |= PlayTimeSessionHelper.CloseOpenSession(Galgame, minuteSessionId);
                ActiveMinuteSessionId = null;
                if (minuteSession is not null && !PlayTimeSessionHelper.HasMinuteSamples(minuteSession))
                {
                    Galgame.PlayTimeSessions.Remove(minuteSession);
                    PlayTimeSessionHelper.RefreshDerivedState(Galgame);
                    changed = true;
                }
            }
        });
        if (changed) await _gameService.SaveGalgameAsync(Galgame!);
    }

    private async Task<Process?> WaitForReplacementProcessAsync(int exitedProcessId)
    {
        if (_directoryPrefix is null) return null;
        _knownProcessIds.Add(exitedProcessId);
        for (int i = 0; i < 5; i++)
        {
            ChangeProgress(0, 1, "RecordPlayTimeTask_WaitingForProcess".GetLocalized(
                Galgame!.Name.Value ?? string.Empty, (5 - i).ToString()));
            await Task.Delay(1000);
            Process? candidate = GameProcessDetector.FindBestProcessInDirectory(_directoryPrefix, _knownProcessIds);
            if (candidate is not null) return candidate;
        }
        return null;
    }

    private Task RecordPlayTimeAsync(CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            bool recordOnlyWhenForeground =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.RecordOnlyWhenForeground);
            _minPlayTimeRecordThreshold =
                await _localSettingsService.ReadSettingAsync<int>(KeyValues.MinPlayTimeRecordThreshold);
            await _recordingReady.Task.WaitAsync(cancellationToken);
            SendRecordingStartedMessage();

            if (PrecisePlayTimeMode != true)
            {
                await RecordLegacyPlayTimeAsync(recordOnlyWhenForeground, cancellationToken);
                return;
            }

            PlayTimeSession? launchSession = ActiveSessionId is { } activeId
                ? Galgame!.PlayTimeSessions.FirstOrDefault(session => session.Id == activeId && session.IsOpen)
                : null;
            if (launchSession is null)
                launchSession = await BeginSessionAsync(DateTime.Now);
            else
                await UiThreadInvokeHelper.InvokeAsync(() =>
                    PlayTimeSessionHelper.EnsureExplicitActivityIntervals(launchSession));
            PlayTimeActivityInterval? activeInterval = null;
            DateTime lastSaveAt = DateTime.Now;

            try
            {
                _localSettingsService.OnSettingChanged += OnSettingChanged;
                while (!_stopped)
                {
                    await Task.Delay(1000, cancellationToken);
                    DateTime now = DateTime.Now;
                    Process? current = _process;
                    bool eligible = !_stopped && current is not null && GameProcessDetector.IsAlive(current) &&
                                    (!recordOnlyWhenForeground ||
                                     (!current.IsMainWindowMinimized() && current.IsMainWindowActive()));

                    if (!eligible)
                    {
                        activeInterval = null;
                        await UiThreadInvokeHelper.InvokeAsync(() => launchSession.EndedAt = now);
                        continue;
                    }

                    if (activeInterval is null)
                    {
                        activeInterval = await BeginActivityIntervalAsync(launchSession, now);
                        lastSaveAt = now;
                        continue;
                    }

                    long sampleSeconds = 0;
                    await UiThreadInvokeHelper.InvokeAsync(() =>
                    {
                        sampleSeconds = PlayTimeSessionHelper.ExtendActivityInterval(
                            Galgame!, launchSession, activeInterval, now);
                        CurrentPlayTimeSeconds = CurrentPlayTimeSeconds > long.MaxValue - sampleSeconds
                            ? long.MaxValue
                            : CurrentPlayTimeSeconds + sampleSeconds;
                        CurrentPlayTime = checked((int)Math.Min(int.MaxValue, CurrentPlayTimeSeconds / 60));
                    });
                    if (sampleSeconds <= 0) continue;

                    if (now - lastSaveAt >= TimeSpan.FromSeconds(15))
                    {
                        await _gameService.SaveGalgameAsync(Galgame!);
                        lastSaveAt = now;
                    }
                }
            }
            finally
            {
                _localSettingsService.OnSettingChanged -= OnSettingChanged;
                await CloseLaunchSessionAsync(launchSession, activeInterval is not null);
            }

            void OnSettingChanged(string key, object? value)
            {
                if (key == KeyValues.RecordOnlyWhenForeground && value is bool b)
                    recordOnlyWhenForeground = b;
                if (key == KeyValues.MinPlayTimeRecordThreshold && value is int i)
                    _minPlayTimeRecordThreshold = i;
            }
        });
    }

    private async Task RecordLegacyPlayTimeAsync(bool recordOnlyWhenForeground, CancellationToken cancellationToken)
    {
        DateTime nextSampleAt = DateTime.Now.AddMinutes(1);
        PlayTimeSession? launchSession = ActiveMinuteSessionId is { } activeId
            ? Galgame!.PlayTimeSessions.FirstOrDefault(session =>
                session.Id == activeId &&
                session.IsOpen &&
                session.Kind == PlayTimeSessionKind.MinuteSampled)
            : null;
        if (launchSession is null)
            launchSession = await BeginMinuteSessionAsync(DateTime.Now);
        try
        {
            _localSettingsService.OnSettingChanged += OnSettingChanged;
            while (!_stopped)
            {
                await Task.Delay(1000, cancellationToken);
                DateTime now = DateTime.Now;
                Process? current = _process;
                if (now < nextSampleAt) continue;

                // 与旧版一致，每次唤醒只采样一次；系统休眠或线程延迟不会追补中间分钟。
                nextSampleAt = now.AddMinutes(1);
                bool eligible = !_stopped && current is not null && GameProcessDetector.IsAlive(current) &&
                                (!recordOnlyWhenForeground ||
                                 (!current.IsMainWindowMinimized() && current.IsMainWindowActive()));
                if (!eligible) continue;

                await UiThreadInvokeHelper.InvokeAsync(() =>
                {
                    PlayTimeSessionHelper.AddLegacyMinuteSample(Galgame!, now, launchSession);
                    if (CurrentPlayTime < int.MaxValue) CurrentPlayTime++;
                    CurrentPlayTimeSeconds = CurrentPlayTimeSeconds > long.MaxValue - 60
                        ? long.MaxValue
                        : CurrentPlayTimeSeconds + 60;
                });
                await _gameService.SaveGalgameAsync(Galgame!);
            }
        }
        finally
        {
            _localSettingsService.OnSettingChanged -= OnSettingChanged;
            await CloseMinuteSessionAsync(launchSession);
        }

        void OnSettingChanged(string key, object? value)
        {
            if (key == KeyValues.RecordOnlyWhenForeground && value is bool b)
                recordOnlyWhenForeground = b;
            if (key == KeyValues.MinPlayTimeRecordThreshold && value is int i)
                _minPlayTimeRecordThreshold = i;
        }
    }

    private async Task<PlayTimeSession> BeginMinuteSessionAsync(DateTime startedAt)
    {
        PlayTimeSession session = new()
        {
            StartedAt = startedAt,
            EndedAt = startedAt,
            IsOpen = true,
            InstallationId = InstallationId,
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = [],
            ActivityIntervals = [],
        };
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            Galgame!.PlayTimeSessions.Add(session);
            ActiveMinuteSessionId = session.Id;
        });
        try
        {
            await _gameService.SaveGalgameAsync(Galgame!);
        }
        catch
        {
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                Galgame!.PlayTimeSessions.RemoveAll(item => item.Id == session.Id);
                if (ActiveMinuteSessionId == session.Id) ActiveMinuteSessionId = null;
                PlayTimeSessionHelper.RefreshDerivedState(Galgame);
            });
            throw;
        }
        return session;
    }

    private async Task CloseMinuteSessionAsync(PlayTimeSession session)
    {
        DateTime endedAt = DateTime.Now;
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            session.EndedAt = endedAt < session.StartedAt ? session.StartedAt : endedAt;
            session.IsOpen = false;
            ActiveMinuteSessionId = null;
            if (!PlayTimeSessionHelper.HasMinuteSamples(session))
                Galgame!.PlayTimeSessions.RemoveAll(item => item.Id == session.Id);
            PlayTimeSessionHelper.RefreshDerivedState(Galgame!);
        });
        await _gameService.SaveGalgameAsync(Galgame!);
    }

    private async Task<PlayTimeSession> BeginSessionAsync(DateTime startedAt)
    {
        PlayTimeSession session = new()
        {
            StartedAt = startedAt,
            EndedAt = startedAt,
            IsOpen = true,
            InstallationId = InstallationId,
            Kind = PlayTimeSessionKind.Native,
            CountsTowardPlayTime = true,
            ActivityIntervals = [],
        };
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            Galgame!.PlayTimeSessions.Add(session);
            ActiveSessionId = session.Id;
        });
        try
        {
            await _gameService.SaveGalgameAsync(Galgame!);
        }
        catch
        {
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                Galgame!.PlayTimeSessions.RemoveAll(item => item.Id == session.Id);
                if (ActiveSessionId == session.Id) ActiveSessionId = null;
                PlayTimeSessionHelper.RefreshDerivedState(Galgame);
            });
            throw;
        }
        return session;
    }

    private async Task<PlayTimeActivityInterval> BeginActivityIntervalAsync(
        PlayTimeSession session, DateTime startedAt)
    {
        PlayTimeActivityInterval? interval = null;
        await UiThreadInvokeHelper.InvokeAsync(() =>
            interval = PlayTimeSessionHelper.BeginActivityInterval(session, startedAt));
        await _gameService.SaveGalgameAsync(Galgame!);
        return interval!;
    }

    private async Task CloseLaunchSessionAsync(PlayTimeSession session, bool extendActiveInterval)
    {
        DateTime endedAt = DateTime.Now;
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            PlayTimeActivityInterval? interval = session.ActivityIntervals?.LastOrDefault();
            if (extendActiveInterval && interval is not null)
            {
                long addedSeconds = PlayTimeSessionHelper.ExtendActivityInterval(
                    Galgame!, session, interval, endedAt);
                CurrentPlayTimeSeconds = CurrentPlayTimeSeconds > long.MaxValue - addedSeconds
                    ? long.MaxValue
                    : CurrentPlayTimeSeconds + addedSeconds;
                CurrentPlayTime = checked((int)Math.Min(int.MaxValue, CurrentPlayTimeSeconds / 60));
            }
            session.EndedAt = endedAt < session.StartedAt ? session.StartedAt : endedAt;
            session.IsOpen = false;
            ActiveSessionId = null;
            PlayTimeSessionHelper.RefreshDerivedState(Galgame!);
        });
        await _gameService.SaveGalgameAsync(Galgame!);
    }

    private void SendRecordingStartedMessage()
    {
        if (_recordingStartedMessageSent || Galgame is null) return;
        _recordingStartedMessageSent = true;
        App.GetService<IMessenger>().Send(new GalgamePlayTimeRecordingStartedMessage(Galgame));
    }

    private void StartRecording()
    {
        RecordingStarted = true;
        _recordingReady.TrySetResult();
        SendRecordingStartedMessage();
        ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame!.Name.Value!));
    }

    private void TryConfirmGameplayWindow()
    {
        if (RecordingStarted != true)
        {
            if (_launchWindowTracker is null) return;
            foreach (GameWindowSnapshot snapshot in _launchWindowTracker.DrainLogSnapshots())
                Log($"Window observed: pid={snapshot.ProcessId}, hwnd=0x{snapshot.WindowHandle:X}, " +
                    $"class={snapshot.ClassName}, size={snapshot.Width}x{snapshot.Height}, title={snapshot.Title}");
            if (_launchWindowTracker.ConfirmedSnapshot is not { } confirmed) return;
            if (confirmed.ProcessId != ProcessId)
            {
                Process? candidate = null;
                try
                {
                    candidate = Process.GetProcessById(confirmed.ProcessId);
                    if (_directoryPrefix is null || !GameProcessDetector.IsProcessInDirectory(candidate, _directoryPrefix))
                    {
                        candidate.Dispose();
                        return;
                    }
                    TrackProcess(candidate);
                }
                catch
                {
                    candidate?.Dispose();
                    return;
                }
            }
            if (_process is null || !GameProcessDetector.IsAlive(_process)) return;
            StartRecording();
            _launchWindowTracker.Stop();
        }
        else if (_confirmedGameplayProcessId == ProcessId || _process is null ||
                 !_directWindowGate.Observe(GameProcessDetector.TryGetPrimaryWindowSnapshot(_process)))
            return;

        _confirmedGameplayProcessId = ProcessId;
        _processRelay.Confirm(_process!);
        Log($"Gameplay process confirmed: pid={ProcessId}");
    }

    private void TryAttachStableForegroundProcess()
    {
        if (_directoryPrefix is null ||
            !string.IsNullOrWhiteSpace(Galgame?.SourceEntries.FirstOrDefault(e => e.EntryId == InstallationId)
                ?.LocalConfig?.ProcessName)) return;
        Process? foreground = GameProcessDetector.TryGetForegroundProcessInDirectory(_directoryPrefix);
        int candidateId = foreground is null ? -1 : GameProcessDetector.SafeGetId(foreground);
        if (_knownProcessIds.Contains(candidateId)) candidateId = -1;
        if (!_foregroundProcessHandoffGate.Observe(ProcessId, candidateId))
        {
            foreground?.Dispose();
            return;
        }
        TrackProcess(foreground!);
        Log($"Process attached from foreground: pid={ProcessId}, process={ProcessName}");
    }

    private void TrackProcess(Process process)
    {
        _process = process;
        _attachedProcesses.Add(process);
        ProcessId = GameProcessDetector.SafeGetId(process);
        _knownProcessIds.Add(ProcessId);
        _confirmedGameplayProcessId = 0;
        _directWindowGate.Reset();
        try
        {
            ProcessName = process.ProcessName;
            ProcessStartTime = process.StartTime;
        }
        catch
        {
            // 读取身份信息失败不代表进程已经退出，仍保留 PID 跟踪和窗口观察。
            ProcessStartTime = null;
        }
        _launchWindowTracker?.TrackProcess(ProcessId);
        _processRelay.Track(process);
    }

    private void Log(string message)
    {
        try
        {
            App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
                $"Play-time: gameUuid={Galgame!.Uuid:D}, installationId={InstallationId:D}, {message}");
        }
        catch
        {
            // 诊断日志失败不能中断计时或进程接力。
        }
    }

    public override bool OnSearch(string key) =>
        Galgame is not null && string.Equals(Galgame.Uuid.ToString("D"), key, StringComparison.OrdinalIgnoreCase);

    public override string Title { get; } = "RecordPlayTimeTask_Title".GetLocalized();
}
