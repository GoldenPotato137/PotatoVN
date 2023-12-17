using CommunityToolkit.WinUI;

namespace GalgameManager.Helpers;

public static class UiThreadInvokeHelper
{
    public static async Task InvokeAsync(Action? action)
    {
        if(action is null) return;
        await App.DispatcherQueue.EnqueueAsync(action);
    }

    public static async Task InvokeAsync(Func<Task> action)
    {
        await App.DispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    public static void Invoke(Func<Task> action)
    {
        App.DispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    public static void Invoke(Action action)
    {
        App.DispatcherQueue.EnqueueAsync(action);
    }
}