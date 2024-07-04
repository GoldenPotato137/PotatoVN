namespace GalgameManager.Models.BgTasks;

public class EmptyBgTask : BgTaskBase
{
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal() => Task.CompletedTask;

    public override string Title { get; } = string.Empty;
}