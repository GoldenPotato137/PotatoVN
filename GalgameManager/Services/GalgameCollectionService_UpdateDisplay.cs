using GalgameManager.Helpers;
using GalgameManager.Models;

namespace GalgameManager.Services;

public partial class GalgameCollectionService
{
    private enum UpdateType
    {
        Add,
        Remove,
        Update,
        Play,
        ApplyFilter,
        ApplySearch,
        Sort,
        Init
    }
    
    /// <summary>
    /// 更新显示列表
    /// </summary>
    /// <param name="type">操作类型</param>
    /// <param name="galgame">如果这个操作是与某个游戏有关的，填入游戏</param>
    private void UpdateDisplay(UpdateType type, Galgame? galgame = null)
    {
        switch (type)
        {
            case UpdateType.Add:
                TryAddToDisplay(galgame!);
                break;
            case UpdateType.Remove:
                TryRemoveFromDisplay(galgame!);
                break;
            case UpdateType.Update:
            case UpdateType.Play:
                TryRemoveFromDisplay(galgame!);
                TryAddToDisplay(galgame!);
                break;
            case UpdateType.Init:
            case UpdateType.Sort:
            case UpdateType.ApplyFilter:
                _displayGalgames.Clear();
                foreach(Galgame tmp in _galgames)
                    TryAddToDisplay(tmp);
                break;
            case UpdateType.ApplySearch:
                foreach (Galgame tmp in _galgames)
                {
                    if(CheckDisplay(tmp) == false && _searchKey!=string.Empty)
                        TryRemoveFromDisplay(tmp);
                    else
                        TryAddToDisplay(tmp);
                }
                break;
        }
    }
    
    /// <summary>
    /// <b>不要手动调用这个函数</b><p/>
    /// 尝试往显示列表中添加一个Galgame<br/>
    /// 若Galgame已经在显示列表中或不应该显示在列表中则不添加
    /// </summary>
    private void TryAddToDisplay(Galgame galgame)
    {
        if (_displayGalgames.Contains(galgame)) return;
        if (CheckDisplay(galgame) == false) return;
        for(var i = 0;i < _displayGalgames.Count;i++) //这里可以用二分查找优化, 暂时不做
            if (galgame.CompareTo(_displayGalgames[i]) >= 0)
            {
                _displayGalgames.Insert(i, galgame);
                return;
            }
        _displayGalgames.Add(galgame);
    }
    
    /// <summary>
    /// <b>不要手动调用这个函数</b><p/>
    /// 尝试从显示列表中移除一个Galgame<br/>
    /// 若Galgame不在显示列表中则什么都不做
    /// </summary>
    private void TryRemoveFromDisplay(Galgame galgame)
    {
        if (_displayGalgames.Contains(galgame) == false) return;
        _displayGalgames.Remove(galgame);
    }

    /// <summary>
    /// <b>不要手动调用这个函数</b><p/>
    /// 检查一个Galgame是否应该显示在列表中<p/>
    /// 具体规则如下：
    /// (若有搜索关键字) 该Galgame是否满足搜索关键字<br/>
    /// (若没有搜索关键字) 该Galgame是否满足Filters条件<br/>
    /// </summary>
    /// <returns></returns>
    private bool CheckDisplay(Galgame galgame)
    {
        if (_searchKey == string.Empty)
            return _filterService.ApplyFilters(galgame);
        return ApplySearchKey(galgame);
    }

    private bool ApplySearchKey(Galgame galgame)
    {
        return galgame.Name.Value!.ContainX(_searchKey) || 
               galgame.Developer.Value!.ContainX(_searchKey) || 
               galgame.Tags.Value!.Any(str => str.ContainX(_searchKey));
    }
}