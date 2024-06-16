using GalgameManager.Helpers;

namespace GalgameManager.Models;

/// <summary>
/// Galgame的UID，用于唯一标识一款游戏 <br/>
/// <para>
/// 当以下<b>任何一个字段</b>相等时，两个GalgameUid对象相等(i.e. ==会返回true)：
/// BangumiID/VndbID/PvnID/CNName/Name
/// </para>
/// </summary>
public class GalgameUid
{
    public string? BangumiId;
    public string? VndbId;
    public string? PvnId;
    public required string Name;
    public string? CnName;

    public static bool operator == (GalgameUid? left, GalgameUid? right)
    {
        if (left is null) return right is null;
        if (right is null) return false;
        if (left.PvnId.IsNullOrEmpty() == false && left.PvnId == right.PvnId) return true;
        if (left.BangumiId.IsNullOrEmpty() == false && left.BangumiId == right.BangumiId) return true;
        if (left.VndbId.IsNullOrEmpty() == false && left.VndbId == right.VndbId) return true;
        if (left.CnName.IsNullOrEmpty() == false && left.CnName == right.CnName) return true;
        return left.Name == right.Name;
    }

    public static bool operator !=(GalgameUid? left, GalgameUid? right) => !(left == right);
    
    public override bool Equals(object? other) => other is GalgameUid uid && this == uid;

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => HashCode.Combine(Name);

    /// <summary>
    /// 与另一个UID的相似度，越多字段相同，相似度越高
    /// </summary>
    /// <param name="rhs"></param>
    /// <returns></returns>
    public int Similarity(GalgameUid? rhs)
    {
        if (rhs is null) return 0;
        var result = 0;
        result += PvnId.IsNullOrEmpty() == false && PvnId == rhs.PvnId ? 1 : 0;
        result += BangumiId.IsNullOrEmpty() == false && BangumiId == rhs.BangumiId ? 1 : 0;
        result += VndbId.IsNullOrEmpty() == false && VndbId == rhs.VndbId ? 1 : 0;
        result += CnName.IsNullOrEmpty() == false && CnName == rhs.CnName ? 1 : 0;
        result += Name == rhs.Name ? 1 : 0;
        return result;
    }
}