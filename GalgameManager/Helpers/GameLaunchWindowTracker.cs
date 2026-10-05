namespace GalgameManager.Helpers;

/// <summary>
/// 从游戏启动前开始保存安装目录中新进程的窗口变化，避免后台任务附着较晚时漏掉启动弹窗。
/// </summary>
public sealed class GameLaunchWindowTracker : IAsyncDisposable
{
    private static readonly TimeSpan InitialObservationInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan StableObservationInterval = TimeSpan.FromMilliseconds(100);
    private readonly object _syncRoot = new();
    private readonly GameWindowTransitionGate _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Queue<GameWindowSnapshot> _pendingLogSnapshots = new();
    private GameWindowSnapshot? _lastLoggedSnapshot;
    private GameWindowSnapshot? _confirmedSnapshot;
    private Task? _observationTask;
    private volatile int _trackedProcessId;

    public GameLaunchWindowTracker(GameWindowSnapshot? baseline = null) => _gate.RestoreBaseline(baseline);

    public GameWindowSnapshot? Baseline
    {
        get
        {
            lock (_syncRoot)
                return _gate.Baseline;
        }
    }

    public void TrackProcess(int processId) => _trackedProcessId = processId;

    public GameWindowTransitionStage Stage
    {
        get
        {
            lock (_syncRoot)
                return _gate.Stage;
        }
    }

    public GameWindowSnapshot? ConfirmedSnapshot
    {
        get
        {
            lock (_syncRoot)
                return _confirmedSnapshot;
        }
    }

    /// <summary>
    /// 在启动操作执行前开始观察，确保短命启动进程退出前后的窗口都能进入同一段历史。
    /// </summary>
    public void Start(string? directoryPrefix, IReadOnlyCollection<int> preExistingProcessIds)
    {
        lock (_syncRoot)
        {
            if (_observationTask is not null) return;
            HashSet<int> excluded = new(preExistingProcessIds);
            _observationTask = Task.Run(async () =>
            {
                try
                {
                    while (!_cancellation.IsCancellationRequested && ConfirmedSnapshot is null)
                    {
                        GameWindowSnapshot? snapshot = directoryPrefix is null ? null :
                            GameProcessDetector.TryGetPrimaryWindowSnapshotInDirectory(directoryPrefix, excluded);
                        // 目录路径不可读取时仍通过已知 PID 观察窗口，所有采样都由本循环执行。
                        snapshot ??= GameProcessDetector.TryGetPrimaryWindowSnapshot(_trackedProcessId);
                        _ = Observe(snapshot);
                        TimeSpan interval = Stage == GameWindowTransitionStage.WaitingForBaseline
                            ? InitialObservationInterval
                            : StableObservationInterval;
                        await Task.Delay(interval, _cancellation.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 启动失败、任务结束或正式窗口已经确认时正常停止观察。
                }
            });
        }
    }

    /// <summary>
    /// 将一次采样并入窗口转换历史；运行时只由观察循环调用。
    /// </summary>
    public bool Observe(GameWindowSnapshot? snapshot)
    {
        lock (_syncRoot)
        {
            if (snapshot is { IsUsable: true } current &&
                (!_lastLoggedSnapshot.HasValue ||
                 GameWindowTransitionGate.HasMeaningfulTransition(_lastLoggedSnapshot.Value, current)))
            {
                _pendingLogSnapshots.Enqueue(current);
                _lastLoggedSnapshot = current;
            }

            if (!_gate.Observe(snapshot) || !snapshot.HasValue) return false;
            _confirmedSnapshot ??= snapshot.Value;
            _cancellation.Cancel();
            return true;
        }
    }

    public IReadOnlyList<GameWindowSnapshot> DrainLogSnapshots()
    {
        lock (_syncRoot)
        {
            List<GameWindowSnapshot> result = [];
            while (_pendingLogSnapshots.TryDequeue(out GameWindowSnapshot snapshot)) result.Add(snapshot);
            return result;
        }
    }

    public void Stop() => _cancellation.Cancel();

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_observationTask is not null) await _observationTask;
        _cancellation.Dispose();
    }
}
