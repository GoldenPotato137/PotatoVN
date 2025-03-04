using GalgameManager.Models;
namespace GalgameManager.Contracts.Services;
public interface IShortcutService
{
    /// <summary>
    /// 添加一个shortcut
    /// </summary>
    /// <param name="shortcut"></param>
    /// <returns></returns>
    public Task AddShortcutAsync(Galgame galgame);
    /// <summary>
    /// 将shortcut更新，一般只更新时间，如果appid不见了，会被认为是从steam中删除了
    /// </summary>
    /// <returns></returns>
    public void UpdateShortcutAsync();
    /// <summary>
    /// 得到所有的Shortcut
    /// </summary>
    /// <returns></returns>
    public Task GetShortcutsAsync();
}