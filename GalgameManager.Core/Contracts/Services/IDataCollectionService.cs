using System.Collections.ObjectModel;

namespace GalgameManager.Core.Contracts.Services;

public interface IDataCollectionService<T>
{
    Task<ObservableCollection<T>> GetContentGridDataAsync();

    /// <summary>
    /// 初始化
    /// </summary>
    /// <returns></returns>
    Task InitAsync();
}
