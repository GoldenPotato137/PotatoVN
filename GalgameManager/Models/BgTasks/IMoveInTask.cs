namespace GalgameManager.Models.BgTasks;

/// <summary>
/// 把游戏物理移入某个库的后台任务
/// </summary>
public interface IMoveInTask
{
    /// 移入完成后游戏在目标库中的路径（游戏文件夹或压缩包），用于登记新条目
    public string ResultPath { get; }
}
