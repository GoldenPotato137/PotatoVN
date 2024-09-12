using System.Collections.ObjectModel;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IGalgameCollectionService
{
    public Task InitAsync();

    public Task StartAsync();

    public Task<AddGalgameResult> AddLocalGameAsync(string path, bool force);

    /// <summary>
    /// 移除一个galgame
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <param name="removeFromDisk">是否要从硬盘移除游戏</param>
    public Task RemoveGalgame(Galgame galgame, bool removeFromDisk = false);

    /// <summary>
    /// 获取所有galgame
    /// </summary>
    public ObservableCollection<Galgame> Galgames { get; }
    
    /// <summary>
    /// 获取UID相似度最高的游戏，若全为0则返回null<br/>
    /// Uid比较规则见：<see cref="GalgameUid"/>
    /// </summary>
    public Galgame? GetGalgameFromUid(GalgameUid uid);

    /// <summary>
    /// 从id获取galgame
    /// </summary>
    /// <param name="id">id</param>
    /// <param name="rssType">id的信息源</param>
    /// <returns>galgame，若找不到返回null</returns>
    public Galgame? GetGalgameFromId(string? id, RssType rssType);

    /// <summary>
    /// 从名字获取galgame
    /// </summary>
    /// <param name="name">名字</param>
    /// <returns>galgame，找不到返回null</returns>
    public Galgame? GetGalgameFromName(string? name);
}