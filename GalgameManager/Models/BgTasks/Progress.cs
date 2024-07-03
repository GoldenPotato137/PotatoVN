using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class Progress
{
    public long Current { get; init; }
    public long Total { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool NotifyWhenSuccess { get; init; }

    public InfoBarSeverity ToSeverity()
    {
        if (Current < 0) return InfoBarSeverity.Error;
        if (Current >= Total) return InfoBarSeverity.Success;
        return InfoBarSeverity.Informational;
    }
}