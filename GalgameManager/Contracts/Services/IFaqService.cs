using System.Collections.ObjectModel;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IFaqService
{
    public event Action UpdateStatusChangeEvent;

    public bool IsUpdating { get; }

    /// <summary>
    /// 获取常见问题列表
    /// </summary>
    /// <param name="forceRefresh">是否忽略刷新缓存强制刷新</param>
    /// <returns>FAQ列表</returns>
    Task<ObservableCollection<Faq>> GetFaqAsync(bool forceRefresh = false);
}