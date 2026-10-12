using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;

namespace GalgameManager.Contracts.Services;

/// <summary>
/// 负责按明确安装实例启动游戏并衔接游玩期间后台任务。
/// </summary>
public interface IGameLaunchService
{
    /// <summary>
    /// 启动指定游戏的指定安装实例。
    /// </summary>
    /// <param name="game">逻辑游戏</param>
    /// <param name="installation">要启动的安装实例</param>
    Task LaunchAsync(Galgame game, GalgameAndPath installation);

    /// <summary>
    /// 添加新启动或恢复的计时任务；同一逻辑游戏已有任务时忽略重复请求。
    /// 返回的任务在计时任务结束后完成。
    /// </summary>
    Task AddPlayTimeTaskAsync(RecordPlayTimeTask task);
}
