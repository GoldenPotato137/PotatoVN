using GalgameManager.Models.BgTasks;

namespace GalgameManager.Contracts.Services;

public interface IBgTaskService
{
    public event Action<BgTaskBase> BgTaskAdded; 
    public event Action<BgTaskBase> BgTaskRemoved; 
    
    /// <summary>
    /// 保存所有后台任务的启动字符串
    /// </summary>
    public void SaveBgTasksString();
    
    /// <summary>
    /// 从启动字符串中解析出后台任务
    /// </summary>
    public Task ResolvedBgTasksAsync();
    
    /// <summary>
    /// 新增后台任务
    /// </summary>
    /// <returns>这个后台任务对应的task</returns>
    public Task AddBgTask(BgTaskBase bgTask);
    
    /// <summary>
    /// 获取所有后台任务
    /// </summary>
    public IEnumerable<BgTaskBase> GetBgTasks();
    
    /// <summary>
    /// 获取指定类型的后台任务，如果没有则返回null
    /// </summary>
    /// <param name="key">关键字</param>
    public T? GetBgTask<T>(string key) where T : BgTaskBase;
}