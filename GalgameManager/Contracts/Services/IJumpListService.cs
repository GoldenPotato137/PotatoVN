using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IJumpListService
{
    /// <summary>
    /// 把这个游戏加入跳转列表，如果已经存在则更新
    /// </summary>
    /// <param name="galgame">galgame</param>
    Task AddToJumpListAsync(Galgame galgame);
    
    /// <summary>
    /// 从跳转列表中移除这个游戏，如果不存在则不做任何操作
    /// </summary>
    /// <param name="galgame">galgame</param>
    Task RemoveFromJumpListAsync(Galgame galgame);


    /// <summary>
    /// 更新jump list，去掉不存在（可能在本地被删除的）galgame
    /// </summary>
    /// <param name="galgames">当前galgame</param>
    Task CheckJumpListAsync(IList<Galgame> galgames);
}
