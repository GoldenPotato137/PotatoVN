namespace GalgameManager.Models.BgTasks;

public abstract class BgTaskBase
{
    // public event Action<int, int, string>? OnProgress;
    
    public abstract Task RecoverFromJson();

    public abstract Task Run();
}