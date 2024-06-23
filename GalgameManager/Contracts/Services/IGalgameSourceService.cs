using GalgameManager.Models;
using GalgameManager.Models.Sources;

namespace GalgameManager.Contracts.Services;

public interface IGalgameSourceService
{
    /// <summary>
    /// 将游戏移入某个库
    /// </summary>
    /// <param name="target">目标库</param>
    /// <param name="game">游戏</param>
    /// <param name="targetPath">目标路径，若为null则表示服务可自行决定路径</param>
    public Task MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null);
    
    /// <summary>
    /// 将游戏移出某个库
    /// </summary>
    /// <param name="target">目标库</param>
    /// <param name="game">游戏</param>
    public Task MoveOutAsync(GalgameSourceBase target, Galgame game);
}