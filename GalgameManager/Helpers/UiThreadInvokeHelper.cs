using CommunityToolkit.WinUI;

namespace GalgameManager.Helpers;

public static class UiThreadInvokeHelper
{
    public static async Task InvokeAsync(Action action)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(action);
    }

    public static async Task InvokeAsync(Func<Task> action)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    public static void Invoke(Func<Task> action)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    public static void Invoke(Action action)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(action);
    }
}