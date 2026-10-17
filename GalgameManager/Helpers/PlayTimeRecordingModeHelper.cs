namespace GalgameManager.Helpers;

/// <summary>
/// 解析一次游戏启动期间保持不变的游玩时间记录模式。
/// </summary>
public static class PlayTimeRecordingModeHelper
{
    /// <summary>
    /// 已锁定的模式优先；旧任务若已经创建精确时段，则恢复为精确模式；其余情况读取当前设置。
    /// </summary>
    public static bool ResolvePreciseMode(bool? lockedMode, bool hasActiveSession, bool settingEnabled) =>
        lockedMode ?? (hasActiveSession || settingEnabled);
}
