using System;

namespace GalgameManager.Enums;

[Flags]
public enum GameParseType
{
    None = 0,
    HeaderImage = 1 << 0,
    Character = 1 << 1,
    /// <summary>
    /// 游玩状态（评论、评分等）。
    /// </summary>
    PlayStatus = 1 << 2,
    /// <summary>
    /// 游戏封面图。
    /// </summary>
    Image = 1 << 3,
    /// <summary>
    /// 游戏信息（如游戏名、发行日期、简介、Tag 等）。
    /// </summary>
    GameInfo = 1 << 4,
    All = int.MaxValue,
}
