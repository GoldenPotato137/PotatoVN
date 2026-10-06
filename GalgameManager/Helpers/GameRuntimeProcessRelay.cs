using System.Diagnostics;

namespace GalgameManager.Helpers;

/// <summary>
/// 共享同一次启动的观察目标、正式游戏进程和结束信号，进程接力只由计时任务判断。
/// </summary>
public sealed class GameRuntimeProcessRelay
{
    private readonly object _syncRoot = new();
    private TaskCompletionSource<Process?> _confirmation =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Process? _currentProcess;
    private Process? _confirmedProcess;
    private bool _completed;

    public Task Completion => _completion.Task;

    /// <summary>
    /// 打开当前观察目标的独立进程对象，由调用方释放；目标未变化或已经结束时返回空。
    /// </summary>
    public Process? OpenCurrentProcess(int knownProcessId = 0)
    {
        lock (_syncRoot)
        {
            if (_completed || _currentProcess is null ||
                GameProcessDetector.SafeGetId(_currentProcess) == knownProcessId) return null;
            return OpenProcess(_currentProcess);
        }
    }

    /// <summary>
    /// 更新观察目标，不代表已经通过弹窗检查或确认进入游戏。
    /// </summary>
    public void Track(Process process)
    {
        lock (_syncRoot)
        {
            if (_completed) return;
            _currentProcess = process;
            _confirmedProcess = null;
            if (_confirmation.Task.IsCompleted)
                _confirmation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// 计时任务是否已经结束，不会再确认新的游戏进程。
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (_syncRoot)
                return _completed;
        }
    }

    /// <summary>
    /// 发布由计时任务确认过的正式游戏进程。
    /// </summary>
    public void Confirm(Process process)
    {
        lock (_syncRoot)
        {
            if (_completed) return;
            _currentProcess = process;
            _confirmedProcess = process;
            _confirmation.TrySetResult(process);
        }
    }

    /// <summary>
    /// 等待正式游戏并打开独立进程对象，由调用方释放；计时任务结束时返回空。
    /// </summary>
    public async Task<Process?> WaitForConfirmationAsync()
    {
        while (true)
        {
            Task<Process?> confirmation;
            lock (_syncRoot)
            {
                if (_completed) return null;
                if (_confirmedProcess is not null) return OpenProcess(_confirmedProcess);
                confirmation = _confirmation.Task;
            }
            Process? confirmed = await confirmation.ConfigureAwait(false);
            lock (_syncRoot)
            {
                if (_completed) return null;
                // 等待者恢复执行前目标可能再次接力，不能把已经失效的确认交给辅助任务。
                if (confirmed is not null && ReferenceEquals(confirmed, _confirmedProcess))
                    return OpenProcess(confirmed);
            }
        }
    }

    private static Process? OpenProcess(Process process)
    {
        try
        {
            // 与结束信号共用同一把锁，保证复制身份前计时任务不会释放原对象。
            return Process.GetProcessById(process.Id);
        }
        catch (ArgumentException)
        {
            // 查询期间进程可能已经退出。
            return null;
        }
        catch (InvalidOperationException)
        {
            // 无有效进程身份时不向辅助任务发布对象。
            return null;
        }
    }

    /// <summary>
    /// 标记本次启动的进程跟踪已经结束。
    /// </summary>
    public void Complete()
    {
        lock (_syncRoot)
        {
            if (_completed) return;
            _completed = true;
            _currentProcess = null;
            _confirmedProcess = null;
            _confirmation.TrySetResult(null);
            _completion.TrySetResult();
        }
    }
}
