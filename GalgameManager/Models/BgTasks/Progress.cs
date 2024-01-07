using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public struct Progress
{
    public long Current;
    public long Total;
    public string Message;

    public InfoBarSeverity ToSeverity()
    {
        if(Current < 0) return InfoBarSeverity.Error;
        if(Current >= Total) return InfoBarSeverity.Success;
        return InfoBarSeverity.Informational;
    }
}