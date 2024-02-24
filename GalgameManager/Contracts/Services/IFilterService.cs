using System.Collections.ObjectModel;
using GalgameManager.Models;
using GalgameManager.Models.Filters;

namespace GalgameManager.Contracts.Services;

public interface IFilterService
{
    /// <summary>
    /// 初始化过滤器
    /// </summary>
    public Task InitAsync();
    
    /// <summary>
    /// 获取所有过滤器
    /// </summary>
    public ObservableCollection<FilterBase> GetFilters();
    
    /// <summary>
    /// 检查是否满足所有过滤器
    /// </summary>
    /// <param name="galgame">待检查游戏</param>
    /// <returns>是否满足</returns>
    public bool ApplyFilters(Galgame galgame);

    /// <summary>
    /// 添加过滤器，如果已存在则不添加
    /// </summary>
    public void AddFilter(FilterBase filter);

    /// <summary>
    /// 移除过滤器，如果不存在则不移除
    /// </summary>
    public void RemoveFilter(FilterBase filter);

    /// <summary>
    /// 移除除了虚拟游戏过滤器以外的所有过滤器
    /// </summary>
    public void ClearFilters();

    /// <summary>
    /// 搜索可能的过滤器
    /// </summary>
    /// <param name="str">当前输入字符串</param>
    public Task<List<FilterBase>> SearchFilters(string str);
    
    /// <summary>
    /// 当过滤器发生变化时触发
    /// </summary>
    event Action OnFilterChanged;
}