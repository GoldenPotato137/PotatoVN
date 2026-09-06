namespace GalgameManager.Enums;

/// <summary>
/// 游戏库中的游戏发生变更时，标识本次变更涉及的数据类型。
/// </summary>
[Flags]
public enum GalgameChangeKind
{
    /// <summary>
    /// 未指定任何变更。
    /// </summary>
    None = 0,

    /// <summary>
    /// 游戏已加入游戏库。
    /// </summary>
    Added = 1 << 0,

    /// <summary>
    /// 游戏名称、开发商、发行日期、简介或标签等元数据发生变更。
    /// </summary>
    Metadata = 1 << 1,

    /// <summary>
    /// 游戏来源或本地安装项发生变更。
    /// </summary>
    SourceEntries = 1 << 2,

    /// <summary>
    /// 游戏封面图或头图发生变更。
    /// </summary>
    Images = 1 << 3,

    /// <summary>
    /// 游戏角色信息发生变更。
    /// </summary>
    Characters = 1 << 4,

    /// <summary>
    /// 游戏的游玩状态、评分或评论发生变更。
    /// </summary>
    PlayStatus = 1 << 5,
}

/// <summary>
/// 游戏变更的来源。
/// </summary>
public enum GalgameChangeOrigin
{
    /// <summary>
    /// 由本地添加、编辑或来源管理等操作引起。
    /// </summary>
    LocalOperation,

    /// <summary>
    /// 由游戏信息解析或刷新引起。
    /// </summary>
    Parser,

    /// <summary>
    /// 由 PotatoVN 云同步数据拉取引起。
    /// </summary>
    PvnSync,
}
