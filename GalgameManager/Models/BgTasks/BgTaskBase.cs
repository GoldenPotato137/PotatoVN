using GalgameManager.Helpers;

namespace GalgameManager.Models.BgTasks;

public abstract class BgTaskBase
{
    /// <summary>
    /// (当前进度，总进度，信息)， 当前进度>=总进度时可以理解为任务完成
    /// </summary>
    public event Action<Progress>? OnProgress;

    public Progress CurrentProgress { get; private set; }
    
    protected bool StartFromBg;

    public Task RecoverFromJson()
    {
        StartFromBg = true;
        return RecoverFromJsonInternal();
    }

    protected abstract Task RecoverFromJsonInternal();

    public abstract Task Run();

    public virtual bool OnSearch(string key) => false;
    
    protected void ChangeProgress(int current, int total, string message)
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