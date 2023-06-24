using GalgameManager.Models;

namespace GalgameManager.Contracts;

public interface ICategory
{
    /// <summary>
    /// 判断某个Galgame是否属于该分类
    /// </summary>
    /// <param name="galgame">游戏</param>
    public bool Belong(Galgame galgame);

    /// <summary>
    /// 分类的名字
    /// </summary>
    public string Name();
}