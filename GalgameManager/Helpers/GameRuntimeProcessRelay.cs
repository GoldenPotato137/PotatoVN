using System.Diagnostics;

namespace GalgameManager.Helpers;

/// <summary>
/// 在同一次游戏启动中共享已经确认的正式游戏进程，避免各后台任务独立判断进程接力。
/// </summary>
public sealed class GameRuntimeProcessRelay
{
    private readonly object _syncRoot = new();
    private readonly TaskCompletionSource<Process?> _firstConfirmation =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Process? _confirmedProcess;
    private bool _completed;

    /// <summary>
    /// 当前已经确认的正式游戏进程；尚未确认时返回 <see langword="null"/>。
    /// </summary>
    public Process? ConfirmedProcess
    {
        get
        {
            lock (_syncRoot)
                return _confirmedProcess;
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
            _confirmedProcess = process;
            _firstConfirmation.TrySetResult(process);
        }
    }

    /// <summary>
    /// 等待第一个正式游戏进程；计时任务未确认进程便结束时返回 <see langword="null"/>。
    /// </summary>
    public Task<Process?> WaitForConfirmationAsync()
    {
        lock (_syncRoot)
        {
            if (_confirmedProcess is not null) return Task.FromResult<Process?>(_confirmedProcess);
            if (_completed) return Task.FromResult<Process?>(null);
            return _firstConfirmation.Task;
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
            _firstConfirmation.TrySetResult(null);
        }
    }
}
