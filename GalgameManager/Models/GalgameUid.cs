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
    
    public override string ToString()
    {
        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(Name)) parts.Add($"Name: {Name}");
        if (!string.IsNullOrWhiteSpace(CnName)) parts.Add($"CnName: {CnName}");
        if (!string.IsNullOrWhiteSpace(BangumiId)) parts.Add($"BangumiId: {BangumiId}");
        if (!string.IsNullOrWhiteSpace(VndbId)) parts.Add($"VndbId: {VndbId}");
        if (!string.IsNullOrWhiteSpace(PvnId)) parts.Add($"PvnId: {PvnId}");

        return $"GalgameUid [{string.Join(", ", parts)}]";
    }
}