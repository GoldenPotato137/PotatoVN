namespace GalgameManager.Helpers;

/// <summary>
/// 用于区分启动弹窗和正式游戏窗口的可见顶层窗口快照。
/// </summary>
public readonly record struct GameWindowSnapshot(
    int ProcessId,
    long WindowHandle,
    string ClassName,
    string Title,
    int Width,
    int Height)
{
    public bool IsUsable => ProcessId > 0 && WindowHandle != 0 && Width > 0 && Height > 0;
}

/// <summary>
/// 启动窗口从首次出现到确认正式游戏的阶段。
/// </summary>
public enum GameWindowTransitionStage
{
    WaitingForBaseline,
    WaitingForTransition,
    ConfirmingGameplayWindow,
    Ready,
}

/// <summary>
/// 在观察到稳定且有意义的顶层窗口切换前保持预备计时状态。
/// 此门控不依赖本地化窗口标题或可执行文件名。
/// </summary>
public sealed class GameWindowTransitionGate
{
    private const int RequiredStableSamples = 2;
    private GameWindowSnapshot? _baseline;
    private int _baselineSamples;
    private GameWindowSnapshot? _candidate;
    private int _candidateSamples;

    public GameWindowTransitionStage Stage { get; private set; } = GameWindowTransitionStage.WaitingForBaseline;

    public bool IsReady => Stage == GameWindowTransitionStage.Ready;

    public GameWindowSnapshot? Baseline => _baseline;

    /// <summary>
    /// 恢复此前已经观察到的启动窗口，重启后继续等待同一窗口的有效切换。
    /// </summary>
    public void RestoreBaseline(GameWindowSnapshot? snapshot)
    {
        if (snapshot is not { IsUsable: true }) return;
        _baseline = snapshot;
        _baselineSamples = RequiredStableSamples;
        ResetCandidate();
    }

    public bool Observe(GameWindowSnapshot? snapshot)
    {
        if (IsReady) return true;
        if (!snapshot.HasValue || !snapshot.Value.IsUsable)
        {
            ResetCandidate();
            return false;
        }

        GameWindowSnapshot current = snapshot.Value;
        if (!_baseline.HasValue)
        {
            _baseline = current;
            // 已识别的标准启动对话框无需重复采样才能成为基线，保留快速确认弹窗的历史。
            _baselineSamples = IsStandardDialog(current) ? RequiredStableSamples : 1;
            Stage = _baselineSamples >= RequiredStableSamples
                ? GameWindowTransitionStage.WaitingForTransition
                : GameWindowTransitionStage.WaitingForBaseline;
            return false;
        }

        bool sameBaselineWindow = IsSameWindowIdentity(_baseline.Value, current);
        if (_baselineSamples < RequiredStableSamples)
        {
            if (sameBaselineWindow)
            {
                if (++_baselineSamples >= RequiredStableSamples)
                    Stage = GameWindowTransitionStage.WaitingForTransition;
            }
            else
            {
                _baseline = current;
                _baselineSamples = 1;
                Stage = GameWindowTransitionStage.WaitingForBaseline;
            }
            ResetCandidate();
            return false;
        }

        if (sameBaselineWindow)
        {
            ResetCandidate();
            return false;
        }

        if (!_candidate.HasValue || !IsSameCandidate(_candidate.Value, current))
        {
            _candidate = current;
            _candidateSamples = 1;
            Stage = GameWindowTransitionStage.ConfirmingGameplayWindow;
            return false;
        }

        if (++_candidateSamples < RequiredStableSamples) return false;
        // 标准 Win32 对话框仍属于启动阶段。启动器先切到声明框时，不能把第一次窗口切换
        // 直接当成正式游戏；将声明框提升为新基准后继续等待下一次非对话框切换。
        if (IsStandardDialog(current))
        {
            _baseline = current;
            _baselineSamples = RequiredStableSamples;
            ResetCandidate();
            return false;
        }
        Stage = GameWindowTransitionStage.Ready;
        return true;
    }

    public static bool HasMeaningfulTransition(GameWindowSnapshot baseline, GameWindowSnapshot current)
    {
        if (!baseline.IsUsable || !current.IsUsable) return false;
        return !IsSameWindowIdentity(baseline, current);
    }

    private static bool IsSameWindowIdentity(GameWindowSnapshot left, GameWindowSnapshot right) =>
        left.ProcessId == right.ProcessId &&
        left.WindowHandle == right.WindowHandle &&
        string.Equals(left.ClassName.Trim(), right.ClassName.Trim(), StringComparison.Ordinal);

    private static bool IsSameCandidate(GameWindowSnapshot left, GameWindowSnapshot right) =>
        left.ProcessId == right.ProcessId &&
        left.WindowHandle == right.WindowHandle &&
        string.Equals(left.ClassName.Trim(), right.ClassName.Trim(), StringComparison.Ordinal) &&
        string.Equals(left.Title.Trim(), right.Title.Trim(), StringComparison.Ordinal) &&
        Math.Abs(left.Width - right.Width) < 16 &&
        Math.Abs(left.Height - right.Height) < 16;

    private static bool IsStandardDialog(GameWindowSnapshot snapshot) =>
        string.Equals(snapshot.ClassName.Trim(), "#32770", StringComparison.Ordinal);

    private void ResetCandidate()
    {
        _candidate = null;
        _candidateSamples = 0;
        if (_baselineSamples >= RequiredStableSamples)
            Stage = GameWindowTransitionStage.WaitingForTransition;
    }
}

/// <summary>
/// 要求另一个前台进程连续保持稳定后，运行中的任务才接替到该进程。
/// </summary>
public sealed class StableProcessHandoffGate
{
    private const int RequiredStableSamples = 2;
    private int _candidateProcessId;
    private int _candidateSamples;

    public bool HasPendingCandidate => _candidateProcessId > 0;

    public bool Observe(int trackedProcessId, int? foregroundProcessId)
    {
        if (!foregroundProcessId.HasValue || foregroundProcessId.Value <= 0 ||
            foregroundProcessId.Value == trackedProcessId)
        {
            Reset();
            return false;
        }

        if (_candidateProcessId != foregroundProcessId.Value)
        {
            _candidateProcessId = foregroundProcessId.Value;
            _candidateSamples = 1;
            return false;
        }

        if (++_candidateSamples < RequiredStableSamples) return false;
        Reset();
        return true;
    }

    private void Reset()
    {
        _candidateProcessId = 0;
        _candidateSamples = 0;
    }
}

/// <summary>
/// 直接启动的游戏窗口连续稳定两个采样周期后予以确认。
/// 与 <see cref="GameWindowTransitionGate"/> 不同，此门控不要求先出现启动器到游戏的窗口切换。
/// </summary>
public sealed class StableGameWindowGate
{
    private const int RequiredStableSamples = 2;
    private GameWindowSnapshot? _candidate;
    private int _candidateSamples;

    public bool Observe(GameWindowSnapshot? snapshot)
    {
        if (!snapshot.HasValue || !snapshot.Value.IsUsable)
        {
            Reset();
            return false;
        }

        GameWindowSnapshot current = snapshot.Value;
        if (!_candidate.HasValue || !IsSameWindow(_candidate.Value, current))
        {
            _candidate = current;
            _candidateSamples = 1;
            return false;
        }

        if (++_candidateSamples < RequiredStableSamples) return false;
        Reset();
        return true;
    }

    public void Reset()
    {
        _candidate = null;
        _candidateSamples = 0;
    }

    private static bool IsSameWindow(GameWindowSnapshot left, GameWindowSnapshot right) =>
        left.ProcessId == right.ProcessId &&
        left.WindowHandle == right.WindowHandle &&
        string.Equals(left.ClassName.Trim(), right.ClassName.Trim(), StringComparison.Ordinal) &&
        Math.Abs(left.Width - right.Width) < 16 &&
        Math.Abs(left.Height - right.Height) < 16;
}

public static class GameSessionExitPolicy
{
    /// <summary>
    /// 尚未确认的启动阶段或已经观察到接力候选时等待替代进程，否则正式游戏 PID 退出后立即结束。
    /// </summary>
    public static bool ShouldWaitForReplacement(bool recordingStarted, int confirmedGameplayProcessId,
        int exitedProcessId, bool pendingHandoff = false) =>
        pendingHandoff || !recordingStarted || confirmedGameplayProcessId <= 0 ||
        confirmedGameplayProcessId != exitedProcessId;
}
