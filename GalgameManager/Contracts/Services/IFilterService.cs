using GalgameManager.Helpers;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IFilterService
{
    /// <summary>
    /// 初始化过滤器
    /// </summary>
    public Task InitAsync();
    
    /// <summary>
    /// 检查是否满足所有过滤器
    /// </summary>
    /// <param name="galgame">待检查游戏</param>
    /// <returns>是否满足</returns>
    public bool ApplyFilters(Galgame galgame);

    /// <summary>
    /// 添加过滤器，如果已存在则不添加
    /// </summary>
    public void AddFilter(IFilter filter);

    /// <summary>
    /// 移除过滤器，如果不存在则不移除
    /// </summary>
    public void RemoveFilter(IFilter filter);
    
    /// <summary>
    /// 当过滤器发生变化时触发
    /// </summary>
    event VoidDelegate OnFilterChanged;
}