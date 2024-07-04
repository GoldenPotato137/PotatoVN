using GalgameManager.Helpers;
using Newtonsoft.Json;

namespace GalgameManager.Models.BgTasks;

public abstract class BgTaskBase
{
    [JsonIgnore] public static BgTaskBase Empty { get; } = new EmptyBgTask();
    
    /// <summary>
    /// (当前进度，总进度，信息)， 当前进度>=总进度时可以理解为任务完成
    /// </summary>
    public event Action<Progress>? OnProgress;

    public Progress CurrentProgress { get; private set; } = new();
    
    [JsonIgnore] public Task Task { get; private set; } = Task.CompletedTask;
    
    protected bool StartFromBg;

    public Task RecoverFromJson()
    {
        StartFromBg = true;
        return RecoverFromJsonInternal();
    }

    protected abstract Task RecoverFromJsonInternal();

    public Task Run()
    {
        Task = RunInternal();
        return Task;
    }

    protected abstract Task RunInternal();

    public virtual bool OnSearch(string key) => false;

    public abstract string Title { get; }

    public bool IsRunning => CurrentProgress.Current < CurrentProgress.Total && CurrentProgress.Current >= 0;
    
    /// <summary>
    /// 修改进度
    /// </summary>
    /// <param name="current">当前进度，若此值低于0则任务任务失败</param>
    /// <param name="total">总进度，若current>=total则认为任务完成</param>
    /// <param name="message">信息</param>
    /// <param name="notifyWhenSuccess">部分任务完成时不需要全局的提醒，若不需要提醒则将此值赋为false</param>
    protected void ChangeProgress(long current, long total, string message,bool notifyWhenSuccess = true)
    {
        CurrentProgress = new Progress
        {
            Current = current,
            Total = total,
            Message = message,
            NotifyWhenSuccess = notifyWhenSuccess
        };
        UiThreadInvokeHelper.Invoke(() =>
        {
            OnProgress?.Invoke(CurrentProgress);
        });
    }
}