using GalgameManager.Helpers;
using Newtonsoft.Json;

namespace GalgameManager.Models.BgTasks;

public abstract class BgTaskBase
{
    /// <summary>
    /// (当前进度，总进度，信息)， 当前进度>=总进度时可以理解为任务完成
    /// </summary>
    public event Action<Progress>? OnProgress;

    public Progress CurrentProgress { get; private set; }
    
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
    
    protected void ChangeProgress(long current, long total, string message)
    {
        CurrentProgress = new Progress
        {
            Current = current,
            Total = total,
            Message = message
        };
        UiThreadInvokeHelper.Invoke(() =>
        {
            OnProgress?.Invoke(CurrentProgress);
        });
    }
}