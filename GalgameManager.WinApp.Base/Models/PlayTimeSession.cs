using System;
using System.Collections.Generic;
using System.Linq;

namespace GalgameManager.Models;

/// <summary>
/// 说明原生游玩时段的创建来源。
/// </summary>
public enum PlayTimeSessionKind
{
    Native = 0,
    Imported = 1,
    Manual = 2,
    MinuteSampled = 3,
}

/// <summary>
/// PotatoVN 的单次游戏启动记录。外层起止时间描述本次启动的生命周期。
/// 精确模式实际计入的时间由 <see cref="ActivityIntervals"/> 保存，分钟模式的柱状图分段
/// 则由 <see cref="SampledMinutesByDay"/> 保存。
/// 时段保存在 <see cref="Galgame"/> 中，旧版逐日分钟汇总仍用于兼容旧客户端和服务端同步。
/// </summary>
public sealed class PlayTimeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public bool IsOpen { get; set; }
    public Guid? InstallationId { get; set; }
    public PlayTimeSessionKind Kind { get; set; } = PlayTimeSessionKind.Native;
    public bool CountsTowardPlayTime { get; set; } = true;

    /// <summary>
    /// 分钟级计时模式下，本次启动在各日期实际贡献的整分钟采样数。
    /// 该字段只用于还原柱状图分段，逐日总时长仍以游戏的分钟汇总为准。
    /// </summary>
    public Dictionary<string, int> SampledMinutesByDay { get; set; } = new();

    /// <summary>
    /// 本次启动中实际计入游玩时间的片段。值为 <see langword="null"/> 表示旧数据，
    /// 此时继续使用 <see cref="StartedAt"/> 到 <see cref="EndedAt"/> 作为单一计时片段；
    /// 空列表则表示已经使用新格式，但本次启动尚未累计有效时间。
    /// </summary>
    public List<PlayTimeActivityInterval>? ActivityIntervals { get; set; }

    public PlayTimeSession Clone() => new()
    {
        Id = Id,
        StartedAt = StartedAt,
        EndedAt = EndedAt,
        IsOpen = IsOpen,
        InstallationId = InstallationId,
        Kind = Kind,
        CountsTowardPlayTime = CountsTowardPlayTime,
        SampledMinutesByDay = SampledMinutesByDay is null
            ? new Dictionary<string, int>()
            : new Dictionary<string, int>(SampledMinutesByDay),
        ActivityIntervals = ActivityIntervals?.Select(interval => interval.Clone()).ToList(),
    };
}

/// <summary>
/// 单次游戏启动中连续计入游玩时间的区间；仅前台计时时，每次暂停与恢复会产生新的内部区间，
/// 但不会拆成新的外层游玩时段。
/// </summary>
public sealed class PlayTimeActivityInterval
{
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }

    public PlayTimeActivityInterval Clone() => new()
    {
        StartedAt = StartedAt,
        EndedAt = EndedAt,
    };
}
