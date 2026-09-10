namespace GalgameManager.Models.BgTasks;

/// <summary>
/// 标记活动期间必须保持逻辑唯一的后台任务。
/// </summary>
public interface IDeduplicatedBgTask
{
    /// <summary>
    /// 获取任务类型内的逻辑标识；空值表示不启用去重。
    /// </summary>
    string? DeduplicationKey { get; }
}
