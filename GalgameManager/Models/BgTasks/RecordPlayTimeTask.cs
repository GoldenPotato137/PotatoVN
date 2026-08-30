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
    private readonly StableProcessHandoffGate _foregroundProcessHandoffGate = new();
    private readonly StableGameWindowGate _directWindowGate = new();
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
        : this(game, process, installationId, null, null)
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
            // 进程对象可能无效（例如通过 ShellExecute 启动的快捷方式），仍创建任务，由目录检查兜底。
        }
        DelayPlayTimeUntilMainWindow = game.SourceEntries.FirstOrDefault(e => e.EntryId == installationId)
            ?.LocalConfig?.DelayPlayTimeUntilMainWindow == true;
        InitDirectoryWatch();
    }

    protected override Task RecoverFromJsonInternal()
    {
        _recoveredFromJson = true;
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        HasPreLaunchProcessSnapshot = false;
        InitDirectoryWatch();
        return Task.CompletedTask;
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
        if(_process is null || Galgame is null) return ;
        ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
        Task t = Task.Run(async () =>
        {
            while (true)
            {
                Process? tracked = _process;
                if (tracked is null) break;
                int trackedProcessId = GameProcessDetector.SafeGetId(tracked);
                while (ReferenceEquals(_process, tracked) && GameProcessDetector.IsAlive(tracked))
                {
                    if (_confirmedGameplayProcessId <= 0)
                    {
                        TryAttachStableForegroundProcess(_foregroundProcessHandoffGate);
                        TryConfirmDirectGameplayProcess(_process);
                    }
                    await Task.Delay(500);
                }

                if (!ReferenceEquals(_process, tracked)) continue;
                if (!GameSessionExitPolicy.ShouldWaitForReplacement(
                        _recordingStarted, _confirmedGameplayProcessId, trackedProcessId))
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
            var windowMode = await _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode);
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                _gameService.SaveGalgameAsync(Galgame);
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
                        TimeToDisplayTimeConverter.Convert(CurrentPlayTime)));
                // 手动通知LastPlayTime属性已更改
                Galgame.RaisePropertyChanged(nameof(Galgame.LastPlayTime));
                if (CurrentPlayTime >= _minPlayTimeRecordThreshold) Galgame!.PlayCount++;
                App.GetService<IMessenger>().Send(new GalgameStoppedMessage(Galgame));
            });
            await App.GetService<IGalgameCollectionService>().SaveGalgameAsync(Galgame);
            if(await App.GetService<ILocalSettingsService>().ReadSettingAsync<bool>(KeyValues.SyncGames))
                App.GetService<IPvnService>().Upload(Galgame, PvnUploadProperties.PlayTime);
        });

        _ = RecordPlayTimeAsync();

        await t;
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
            var recordOnlyWhenForeground =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.RecordOnlyWhenForeground);
            _minPlayTimeRecordThreshold = await _localSettingsService.ReadSettingAsync<int>(KeyValues.MinPlayTimeRecordThreshold);
            if (!await WaitForPlayableWindowAsync()) return;
            try
            {
                _localSettingsService.OnSettingChanged += OnSettingChanged;

                while (!_stopped)
                {
                    Thread.Sleep(1000 * 60);
                    Process? current = _process;
                    if (_stopped || current is null || !GameProcessDetector.IsAlive(current) ||
                        (recordOnlyWhenForeground && (current.IsMainWindowMinimized() || !current.IsMainWindowActive())))
                        continue;
                    UiThreadInvokeHelper.Invoke(() =>
                    {
                        Galgame!.TotalPlayTime++;
                        CurrentPlayTime++;
                        _gameService.SaveGalgameAsync(Galgame);
                    });
                    var now = DateTime.Now.ToStringDefault();
                    if (!Galgame!.PlayedTime.TryAdd(now, 1))
                        Galgame.PlayedTime[now]++;
                }
            }
            finally
            {
                _localSettingsService.OnSettingChanged -= OnSettingChanged;
            }

            return;

            void OnSettingChanged(string key, object? value)
            {
                if(key == KeyValues.RecordOnlyWhenForeground && value is bool b)
                    recordOnlyWhenForeground = b;
                if(key == KeyValues.MinPlayTimeRecordThreshold && value is int i)
                    _minPlayTimeRecordThreshold = i;
            }
        });
    }

    public override bool OnSearch(string key) =>
        Galgame is not null && string.Equals(Galgame.Uuid.ToString("D"), key, StringComparison.OrdinalIgnoreCase);

    private void TryConfirmDirectGameplayProcess(Process? process)
    {
        if (!_recordingStarted || process is null || !GameProcessDetector.IsAlive(process)) return;
        // 启用弹窗等待的安装实例只能由窗口切换门控确认。恢复任务缺少启动前快照，
        // 此时将稳定存在的游戏窗口作为最安全的确认依据。
        if (DelayPlayTimeUntilMainWindow && !_recoveredFromJson) return;

        GameWindowSnapshot? snapshot = GameProcessDetector.TryGetPrimaryWindowSnapshot(process);
        if (!_directWindowGate.Observe(snapshot) || !snapshot.HasValue) return;
        if (_confirmedGameplayProcessId == snapshot.Value.ProcessId) return;

        _confirmedGameplayProcessId = snapshot.Value.ProcessId;
        PublishGameplayProcess(process, _confirmedGameplayProcessId);
        App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            $"Gameplay process confirmed by stable window: gameUuid={Galgame!.Uuid:D}, " +
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
                Process? confirmedProcess = _process;
                if (confirmedProcess is not null)
                    PublishGameplayProcess(confirmedProcess, _confirmedGameplayProcessId);
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

    private void PublishGameplayProcess(Process process, int processId)
    {
        if (processId > 0 && GameProcessDetector.SafeGetId(process) == processId)
            _processRelay?.Confirm(process);
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
        _confirmedGameplayProcessId = 0;
        _directWindowGate.Reset();
        try
        {
            ProcessName = process.ProcessName;
        }
        catch
        {
            // 进程可能在接替期间退出，后续生命周期检查会继续处理。
        }
        lock (_knownProcessIdsLock)
            _knownProcessIds?.Add(GameProcessDetector.SafeGetId(process));
        ChangeProgress(0, 1, _recordingStarted
            ? "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame!.Name.Value!)
            : "RecordPlayTimeTask_WaitingForMainWindow".GetLocalized(Galgame!.Name.Value ?? string.Empty));
        App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            $"Play-time process attached: gameUuid={Galgame.Uuid:D}, installationId={InstallationId:D}, " +
            $"previousPid={previousProcessId}, pid={GameProcessDetector.SafeGetId(process)}, " +
            $"process={ProcessName}, reason={reason}");
    }

    private void LogWindowObservation(GameWindowSnapshot snapshot)
    {
        App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            $"Play-time window observed: gameUuid={Galgame!.Uuid:D}, installationId={InstallationId:D}, " +
            $"pid={snapshot.ProcessId}, hwnd=0x{snapshot.WindowHandle:X}, class={snapshot.ClassName}, " +
            $"size={snapshot.Width}x{snapshot.Height}, title={snapshot.Title}");
    }

    public override string Title { get; } = "RecordPlayTimeTask_Title".GetLocalized();
}
