using GalgameManager.Models.BgTasks;

namespace GalgameManager.Contracts.Services;

public interface IBgTaskService
{
    /// <summary>
    /// 保存所有后台任务的启动字符串
    /// </summary>
    public void SaveBgTasksString();
    
    /// <summary>
    /// 从启动字符串中解析出后台任务
    /// </summary>
    public Task ResolvedBgTasksAsync();
    
    /// <summary>
    /// 新增记录游玩时间的后台任务
    /// </summary>
    /// <returns>这个后台任务对应的task</returns>
    public Task AddBgTask(BgTaskBase bgTask);
}