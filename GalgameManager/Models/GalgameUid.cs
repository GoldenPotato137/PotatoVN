using GalgameManager.Helpers;

namespace GalgameManager.Models;

/// <summary>
/// Galgame的UID，用于唯一标识一款游戏 <br/>
/// <para>
/// 使用其Similarity方法可以计算与另一个UID的相似度，判断是否为同一款游戏
/// </para>
/// </summary>
public class GalgameUid
{
    public string? BangumiId { get; init; }
    public string? VndbId { get; init; }
    public string? PvnId { get; init; }
    public required string Name { get; init; }
    public string? CnName { get; init; }
    
    /// <summary>
    /// 与另一个UID的相似度，越多字段相同，相似度越高
    /// </summary>
    /// <param name="rhs"></param>
    /// <returns></returns>
    public int Similarity(GalgameUid? rhs)
    {
        if (rhs is null) return 0;
        var result = 0;
        result += !PvnId.IsNullOrEmpty() && PvnId == rhs.PvnId ? 1 : 0;
        result += !BangumiId.IsNullOrEmpty() && BangumiId == rhs.BangumiId ? 1 : 0;
        result += !VndbId.IsNullOrEmpty() && VndbId == rhs.VndbId ? 1 : 0;
        result += !CnName.IsNullOrEmpty() && CnName == rhs.CnName ? 1 : 0;
        result += Name == rhs.Name ? 1 : 0;
        return result;
    }
}