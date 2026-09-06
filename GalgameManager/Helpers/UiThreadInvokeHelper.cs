using System.Runtime.InteropServices;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;

namespace GalgameManager.Helpers;

public static class UiThreadInvokeHelper
{
    private static DispatcherQueue? _dispatcherQueue;
    private static bool _dispatcherQueueResolved;

    /// 非App上下文（如单元测试，App静态构造因缺少WinAppSDK运行时失败）时返回null
    private static DispatcherQueue? DispatcherQueue
    {
        get
        {
            if (_dispatcherQueueResolved) return _dispatcherQueue;
            try
            {
                _dispatcherQueue = App.DispatcherQueue;
            }
            catch (TypeInitializationException)
            {
                _dispatcherQueue = null;
            }
            _dispatcherQueueResolved = true;
            return _dispatcherQueue;
        }
    }

    public static async Task InvokeAsync(Action? action)
    {
        if(action is null) return;
        if (DispatcherQueue is { } queue)
        {
            await queue.EnqueueAsync(action);
            return;
        }
        action(); // 无UI线程上下文（如单元测试）时降级为同步执行
    }

    public static async Task InvokeAsync(Func<Task> action)
    {
        if (DispatcherQueue is { } queue)
        {
            await queue.EnqueueAsync(async () =>
            {
                await action();
            });
            return;
        }
        await action();
    }

    public static void Invoke(Func<Task> action)
    {
        if (DispatcherQueue is { } queue)
        {
            queue.EnqueueAsync(async () =>
            {
                await action();
            });
            return;
        }
        _ = action();
    }

    public static void Invoke(Action action)
    {
        if (DispatcherQueue is { } queue)
        {
            queue.EnqueueAsync(action);
            return;
        }
        action();
    }

    public static void IgnoreComException(Action action)
    {
        try
        {
            action();
        }
        catch (COMException)
        {
            // ignored
        }
    }
    
    public static void IgnoreComException<T1>(Action<T1> action, T1 arg1)
    {
        try
        {
            action(arg1);
        }
        catch (COMException)
        {
            // ignored
        }
    }
}