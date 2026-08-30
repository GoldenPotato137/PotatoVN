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

public class RecordPlayTimeTask : BgTaskBase, IDeduplicatedBgTask
{
    private const int ManuallySelectProcessSec = 15; //认定为需要手动选择游戏进程的时间阈值

    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }= DateTime.Now;
    public int CurrentPlayTime { get; set; } //本次游玩时间
    public long CurrentPlayTimeSeconds { get; set; } // 本次实际累计秒数
    public bool? PrecisePlayTimeMode { get; set; } // 本次启动锁定的计时模式，恢复任务时保持不变
    public Guid? ActiveSessionId { get; set; } // 异常退出/托盘恢复时继续同一原生时段
    public Guid? ActiveMinuteSessionId { get; set; } // 异常退出/托盘恢复时继续同一条分钟级启动分段
    public Guid? InstallationId { get; set; } // 本次游玩使用的安装实例Id
    public bool HasPreLaunchProcessSnapshot { get; set; } // 是否已经在启动游戏前采集安装目录进程快照
    public List<int> PreExistingProcessIds { get; set; } = []; // 启动前已经存在于安装目录的进程Id
    public bool DelayPlayTimeUntilMainWindow { get; set; } // 等待声明/启动器窗口切换后再开始计时
    public override bool ProgressOnTrayIcon => true;
    public string? DeduplicationKey => Galgame?.Uuid.ToString("D");

    public Galgame? Galgame;
    private volatile Process? _process;
    private string? _directoryPrefix; // 安装目录前缀，用于退出后探测新出现的游戏进程
    private HashSet<int>? _knownProcessIds; // 开始跟踪时已存在于安装目录的进程，复查时只附着新出现的进程
    private readonly object _knownProcessIdsLock = new();
    private volatile bool _stopped;
    private bool _recoveredFromJson;
    private volatile bool _recordingStarted;
    private volatile int _confirmedGameplayProcessId;
    private readonly StableGameWindowGate _directWindowGate = new();
    private readonly StableProcessHandoffGate _foregroundProcessHandoffGate = new();
    private bool _recordingStartedMessageSent;
    private GameRuntimeProcessRelay? _processRelay;
    private GameWindowSnapshot? _initialWindowSnapshot;
    private GameLaunchWindowTracker? _launchWindowTracker;

    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();
    private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
    private int _minPlayTimeRecordThreshold;

    public RecordPlayTimeTask(){}

    /// <summary>
    /// 使用游戏的首选安装实例创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">要跟踪的游戏进程</param>
    public RecordPlayTimeTask(Galgame game, Process process)
        : this(game, process, game.PreferredInstallationId)
    {
    }

    /// <summary>
    /// 使用明确安装实例创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">要跟踪的游戏进程</param>
    /// <param name="installationId">本次游玩使用的安装实例Id</param>
    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId)
        : this(game, process, installationId, null)
    {
    }

    /// <summary>
    /// 使用启动前进程快照创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">启动操作返回的初始进程</param>
    /// <param name="installationId">本次游玩使用的安装实例Id</param>
    /// <param name="preExistingProcessIds">启动前已经存在于安装目录的进程Id</param>
    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId,
        IReadOnlyCollection<int>? preExistingProcessIds)
        : this(game, process, installationId, preExistingProcessIds, null)
    {
    }

    /// <summary>
    /// 使用启动前进程快照和运行时进程接力创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">启动操作返回的初始进程</param>
    /// <param name="installationId">本次游玩使用的安装实例Id</param>
    /// <param name="preExistingProcessIds">启动前已经存在于安装目录的进程Id</param>
    /// <param name="processRelay">向同一启动会话的辅助任务发布正式游戏进程</param>
    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId,
        IReadOnlyCollection<int>? preExistingProcessIds, GameRuntimeProcessRelay? processRelay)
        : this(game, process, installationId, preExistingProcessIds, processRelay, null)
    {
    }

    /// <summary>
    /// 使用启动前进程快照、运行时接力和任务创建时的窗口快照创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">启动操作返回的初始进程</param>
    /// <param name="installationId">本次游玩使用的安装实例Id</param>
    /// <param name="preExistingProcessIds">启动前已经存在于安装目录的进程Id</param>
    /// <param name="processRelay">向同一启动会话的辅助任务发布正式游戏进程</param>
    /// <param name="initialWindowSnapshot">创建后台任务前捕获到的首个窗口</param>
    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId,
        IReadOnlyCollection<int>? preExistingProcessIds, GameRuntimeProcessRelay? processRelay,
        GameWindowSnapshot? initialWindowSnapshot)
        : this(game, process, installationId, preExistingProcessIds, processRelay, initialWindowSnapshot, null)
    {
    }

    /// <summary>
    /// 使用完整启动上下文创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">启动操作返回的初始进程</param>
    /// <param name="installationId">本次游玩使用的安装实例Id</param>
    /// <param name="preExistingProcessIds">启动前已经存在于安装目录的进程Id</param>
    /// <param name="processRelay">向同一启动会话的辅助任务发布正式游戏进程</param>
    /// <param name="initialWindowSnapshot">创建后台任务前捕获到的首个窗口</param>
    /// <param name="launchWindowTracker">从启动动作前开始保存窗口变化的追踪器</param>
    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId,
        IReadOnlyCollection<int>? preExistingProcessIds, GameRuntimeProcessRelay? processRelay,
        GameWindowSnapshot? initialWindowSnapshot, GameLaunchWindowTracker? launchWindowTracker)
    {
        Debug.Assert(game.IsLocalGame);
        Galgame = game;
        _process = process;
        _processRelay = processRelay;
        _initialWindowSnapshot = initialWindowSnapshot;
        _launchWindowTracker = launchWindowTracker;
        InstallationId = installationId;
        HasPreLaunchProcessSnapshot = preExistingProcessIds is not null;
        PreExistingProcessIds = preExistingProcessIds?.ToList() ?? [];
        try
        {
            ProcessName = process.ProcessName;
        }
        catch
        {
            // 进程对象可能无效（例如通过ShellExecute启动的快捷方式），仍创建任务，由退出后的目录检查兜底
        }
        DelayPlayTimeUntilMainWindow = game.SourceEntries.FirstOrDefault(e => e.EntryId == installationId)
            ?.LocalConfig?.DelayPlayTimeUntilMainWindow == true;
        InitDirectoryWatch();
    }

    protected override Task RecoverFromJsonInternal()
    {
        _recoveredFromJson = true;
        HasPreLaunchProcessSnapshot = false;
        InitDirectoryWatch();
        _process = FindRecoveryProcess();
        return Task.CompletedTask;
    }

    private Process? FindRecoveryProcess()
    {
        Process? directoryProcess = _directoryPrefix is null
            ? null
            : GameProcessDetector.FindBestProcessInDirectory(
                _directoryPrefix,
                requiredProcessName: ProcessName);
        if (directoryProcess is not null) return directoryProcess;

        Process[] candidates = string.IsNullOrWhiteSpace(ProcessName)
            ? []
            : Process.GetProcessesByName(ProcessName);
        Process? result = null;
        foreach (Process candidate in candidates)
        {
            if (result is null || IsBetterRecoveryCandidate(candidate, result))
            {
                result?.Dispose();
                result = candidate;
            }
            else
            {
                candidate.Dispose();
            }
        }
        return result;

        static bool IsBetterRecoveryCandidate(Process candidate, Process current)
        {
            bool candidateHasWindow = GameProcessDetector.HasWindow(candidate);
            bool currentHasWindow = GameProcessDetector.HasWindow(current);
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
            _launchWindowTracker?.Stop();
            _processRelay?.Complete();
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
            PrecisePlayTimeMode,
            ActiveSessionId.HasValue,
            await _localSettingsService.ReadSettingAsync<bool>(KeyValues.PrecisePlayTime));
        ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));

        Task processMonitor = Task.Run(async () =>
        {
            while (true)
            {
                Process? tracked = _process;
                if (tracked is null) break;
                int trackedProcessId = GameProcessDetector.SafeGetId(tracked);
                while (ReferenceEquals(_process, tracked) && GameProcessDetector.IsAlive(tracked))
                {
                    if (_confirmedGameplayProcessId <= 0)
                        TryAttachStableForegroundProcess(_foregroundProcessHandoffGate);
                    await Task.Delay(500);
                }

                // 预备计时可能已经主动接替到一个仍在运行的前台游戏进程。
                if (!ReferenceEquals(_process, tracked)) continue;

                // 已确认进入正式游戏的进程退出后应立即结束（#709）；仍处于启动/弹窗/接力阶段时
                // 才保留5秒目录扫描，兼容汉化启动器拉起不同exe的场景。
                if (!GameSessionExitPolicy.ShouldWaitForReplacement(
                        _recordingStarted,
                        _confirmedGameplayProcessId,
                        trackedProcessId))
                {
                    App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
                        $"Confirmed gameplay process exited; finishing immediately: gameUuid={Galgame.Uuid:D}, " +
                        $"installationId={InstallationId:D}, pid={trackedProcessId}");
                    break;
                }

                Process? next;
                try
                {
                    next = await WaitForReplacementProcessAsync(trackedProcessId);
                }
                catch
                {
                    next = null;
                }

                if (!ReferenceEquals(_process, tracked))
                {
                    next?.Dispose();
                    continue;
                }
                if (next is null) break;
                AttachProcess(next, "previous process exited");
            }
            _stopped = true;
        });

        Task recording = RecordPlayTimeAsync();
        await processMonitor;
        await recording;

        WindowMode windowMode = await _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode);
        bool precisePlayTime = PrecisePlayTimeMode == true;
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            GalgamePageParameter parma = new()
            {
                Galgame = Galgame,
                SelectProgress = DateTime.Now - StartTime < TimeSpan.FromSeconds(ManuallySelectProcessSec)
                                 && Galgame.SourceEntries.FirstOrDefault(e => e.EntryId == InstallationId)
                                     ?.LocalConfig?.ProcessName is null
            };
            if (windowMode == WindowMode.SystemTray)
                App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, parma);
            App.SetWindowMode(WindowMode.Normal);
            ChangeProgress(1, 1,
                "RecordPlayTimeTask_Done".GetLocalized(Galgame.Name.Value ?? string.Empty,
                    precisePlayTime
                        ? TimeToDisplayTimeConverter.ConvertSeconds(CurrentPlayTimeSeconds)
                        : TimeToDisplayTimeConverter.Convert(CurrentPlayTime)));
            Galgame.RaisePropertyChanged(nameof(Galgame.LastPlayTime));
            bool reachesPlayCountThreshold = precisePlayTime
                ? CurrentPlayTimeSeconds > 0 &&
                  CurrentPlayTimeSeconds >= _minPlayTimeRecordThreshold * 60L
                : CurrentPlayTime >= _minPlayTimeRecordThreshold;
            if (reachesPlayCountThreshold) Galgame.PlayCount++;
            App.GetService<IMessenger>().Send(new GalgameStoppedMessage(Galgame));
        });
        await _gameService.SaveGalgameAsync(Galgame);
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGames))
            App.GetService<IPvnService>().Upload(Galgame, PvnUploadProperties.PlayTime);
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

    /// <summary>
    /// 启动器类游戏中，被跟踪进程退出后真正的游戏进程才刚被拉起。
    /// 退出后做5秒检查（每秒一次），若安装目录内出现了新进程则返回该进程。
    /// </summary>
    private async Task<Process?> WaitForReplacementProcessAsync(int exitedProcessId)
    {
        if (_directoryPrefix is null || _knownProcessIds is null) return null;
        lock (_knownProcessIdsLock)
            _knownProcessIds.Add(exitedProcessId);
        for (int i = 0; i < 5; i++)
        {
            ChangeProgress(0, 1,
                "RecordPlayTimeTask_WaitingForProcess".GetLocalized(Galgame!.Name.Value ?? string.Empty,
                    (5 - i).ToString()));
            await Task.Delay(1000);
            int[] excludedProcessIds;
            lock (_knownProcessIdsLock)
                excludedProcessIds = _knownProcessIds.ToArray();
            Process? candidate =
                GameProcessDetector.FindBestProcessInDirectory(_directoryPrefix, excludedProcessIds);
            if (candidate is not null) return candidate;
        }
        return null;
    }

    private Task RecordPlayTimeAsync()
    {
        return Task.Run(async () =>
        {
            bool recordOnlyWhenForeground =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.RecordOnlyWhenForeground);
            _minPlayTimeRecordThreshold =
                await _localSettingsService.ReadSettingAsync<int>(KeyValues.MinPlayTimeRecordThreshold);
            if (!await WaitForPlayableWindowAsync()) return;
            SendRecordingStartedMessage();

            if (PrecisePlayTimeMode != true)
            {
                await RecordLegacyPlayTimeAsync(recordOnlyWhenForeground);
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
                    await Task.Delay(1000);
                    DateTime now = DateTime.Now;
                    Process? current = _process;
                    TryConfirmDirectGameplayProcess(current);
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

    private async Task RecordLegacyPlayTimeAsync(bool recordOnlyWhenForeground)
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
                await Task.Delay(1000);
                DateTime now = DateTime.Now;
                Process? current = _process;
                TryConfirmDirectGameplayProcess(current);
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
        await _gameService.SaveGalgameAsync(Galgame!);
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
        await _gameService.SaveGalgameAsync(Galgame!);
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

    private void TryConfirmDirectGameplayProcess(Process? process)
    {
        if (!_recordingStarted || process is null || !GameProcessDetector.IsAlive(process)) return;
        // 启用弹窗等待的安装实例只能由下方切换门控确认。恢复任务缺少启动前快照，
        // 此时将稳定存在的游戏窗口作为最安全的确认依据。
        if (DelayPlayTimeUntilMainWindow && !_recoveredFromJson) return;

        GameWindowSnapshot? snapshot = GameProcessDetector.TryGetPrimaryWindowSnapshot(process);
        if (!_directWindowGate.Observe(snapshot) || !snapshot.HasValue) return;
        if (_confirmedGameplayProcessId == snapshot.Value.ProcessId) return;
        _confirmedGameplayProcessId = snapshot.Value.ProcessId;
        SendGameplayProcessConfirmedMessage(_confirmedGameplayProcessId);
        App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            $"Direct gameplay process confirmed by stable window: gameUuid={Galgame!.Uuid:D}, " +
            $"installationId={InstallationId:D}, pid={_confirmedGameplayProcessId}");
    }

    private async Task<bool> WaitForPlayableWindowAsync()
    {
        // 恢复任务可能已经附着到正式游戏窗口，但无法还原启动前快照；
        // 若仍等待窗口切换，PotatoVN 重启后可能永远无法开始计时。
        if (!DelayPlayTimeUntilMainWindow || _recoveredFromJson)
        {
            _recordingStarted = true;
            return true;
        }

        bool hasPreLaunchTracker = _launchWindowTracker is not null;
        GameLaunchWindowTracker tracker = _launchWindowTracker ??= new GameLaunchWindowTracker();
        if (_initialWindowSnapshot is { } initialSnapshot)
        {
            _ = tracker.Observe(initialSnapshot);
            _initialWindowSnapshot = null;
        }
        ChangeProgress(0, 1,
            "RecordPlayTimeTask_WaitingForMainWindow".GetLocalized(Galgame!.Name.Value ?? string.Empty));
        while (!_stopped)
        {
            Process? current = _process;
            GameWindowSnapshot? snapshot = current is not null && GameProcessDetector.IsAlive(current)
                ? GameProcessDetector.TryGetPrimaryWindowSnapshot(current)
                : null;
            // 启动前追踪器会同时观察安装目录中的替代进程；当前短命进程没有窗口，
            // 不能用它的空快照清除另一个进程中已经捕获到的正式窗口候选。
            if (snapshot.HasValue || !hasPreLaunchTracker) _ = tracker.Observe(snapshot);
            foreach (GameWindowSnapshot observed in tracker.DrainLogSnapshots()) LogWindowObservation(observed);

            GameWindowSnapshot? confirmedSnapshot = tracker.ConfirmedSnapshot;
            if (confirmedSnapshot.HasValue && TryAttachConfirmedGameplayProcess(confirmedSnapshot.Value))
            {
                _recordingStarted = true;
                _confirmedGameplayProcessId = confirmedSnapshot.Value.ProcessId;
                SendGameplayProcessConfirmedMessage(_confirmedGameplayProcessId);
                ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
                App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
                    $"Play-time recording started after window transition: gameUuid={Galgame.Uuid:D}, " +
                    $"installationId={InstallationId:D}, pid={_confirmedGameplayProcessId}");
                return true;
            }
            await Task.Delay(100);
        }
        return false;
    }

    private bool TryAttachConfirmedGameplayProcess(GameWindowSnapshot confirmedSnapshot)
    {
        Process? current = _process;
        if (current is not null && GameProcessDetector.SafeGetId(current) == confirmedSnapshot.ProcessId)
            return GameProcessDetector.IsAlive(current);

        Process? confirmedProcess = null;
        try
        {
            confirmedProcess = Process.GetProcessById(confirmedSnapshot.ProcessId);
            if (!GameProcessDetector.IsAlive(confirmedProcess) ||
                (_directoryPrefix is not null &&
                 !GameProcessDetector.IsProcessInDirectory(confirmedProcess, _directoryPrefix)))
            {
                confirmedProcess.Dispose();
                return false;
            }
            AttachProcess(confirmedProcess, "gameplay window confirmed from launch history");
            return true;
        }
        catch
        {
            confirmedProcess?.Dispose();
            return false;
        }
    }

    private void SendGameplayProcessConfirmedMessage(int processId)
    {
        if (Galgame is null || processId <= 0) return;
        Process? current = _process;
        if (current is not null && GameProcessDetector.SafeGetId(current) == processId)
            _processRelay?.Confirm(current);
    }

    private void TryAttachStableForegroundProcess(StableProcessHandoffGate handoffGate)
    {
        if (_directoryPrefix is null) return;
        Process? foreground = GameProcessDetector.TryGetForegroundProcessInDirectory(_directoryPrefix);
        if (foreground is null)
        {
            int currentProcessId = _process is null ? -1 : GameProcessDetector.SafeGetId(_process);
            _ = handoffGate.Observe(currentProcessId, null);
            return;
        }

        int trackedProcessId = _process is null ? -1 : GameProcessDetector.SafeGetId(_process);
        int foregroundProcessId = GameProcessDetector.SafeGetId(foreground);
        bool existedBeforeLaunch;
        lock (_knownProcessIdsLock)
            existedBeforeLaunch = _knownProcessIds?.Contains(foregroundProcessId) == true;
        if (existedBeforeLaunch)
        {
            _ = handoffGate.Observe(trackedProcessId, null);
            foreground.Dispose();
            return;
        }
        if (!handoffGate.Observe(trackedProcessId, foregroundProcessId))
        {
            foreground.Dispose();
            return;
        }

        AttachProcess(foreground, "stable foreground process in installation directory");
    }

    private void AttachProcess(Process process, string reason)
    {
        int previousProcessId = _process is null ? -1 : GameProcessDetector.SafeGetId(_process);
        _process = process;
        ProcessName = process.ProcessName;
        _confirmedGameplayProcessId = 0;
        _directWindowGate.Reset();
        lock (_knownProcessIdsLock)
            _knownProcessIds?.Add(process.Id);
        ChangeProgress(0, 1, _recordingStarted
            ? "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame!.Name.Value!)
            : "RecordPlayTimeTask_WaitingForMainWindow".GetLocalized(Galgame!.Name.Value ?? string.Empty));
        App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            $"Play-time process attached: gameUuid={Galgame.Uuid:D}, installationId={InstallationId:D}, " +
            $"previousPid={previousProcessId}, pid={process.Id}, process={ProcessName}, reason={reason}");
    }

    private void LogWindowObservation(GameWindowSnapshot snapshot)
    {
        App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            $"Play-time window observed: gameUuid={Galgame!.Uuid:D}, installationId={InstallationId:D}, " +
            $"pid={snapshot.ProcessId}, hwnd=0x{snapshot.WindowHandle:X}, class={snapshot.ClassName}, " +
            $"size={snapshot.Width}x{snapshot.Height}, title={snapshot.Title}");
    }

    public override bool OnSearch(string key) =>
        Galgame is not null && string.Equals(Galgame.Uuid.ToString("D"), key, StringComparison.OrdinalIgnoreCase);

    public override string Title { get; } = "RecordPlayTimeTask_Title".GetLocalized();
}
