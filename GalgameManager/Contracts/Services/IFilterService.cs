using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IFilterService
{
    /// <summary>
    /// 检查是否满足所有过滤器
    /// </summary>
    /// <param name="galgame">待检查游戏</param>
    /// <returns>是否满足</returns>
    public bool ApplyFilters(Galgame galgame);
}