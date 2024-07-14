using System.Collections.ObjectModel;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IGalgameCollectionService
{
    public Task InitAsync();

    public Task StartAsync();
    
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