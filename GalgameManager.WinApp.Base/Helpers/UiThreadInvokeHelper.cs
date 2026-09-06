using System;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;

namespace GalgameManager.WinApp.Base.Helpers;

public static class UiThreadInvokeHelper
{
    private static DispatcherQueue _dispatcherQueue = null!;
    
    [Obsolete("这个方法只会被App初始化时调用，请不要在其他地方调用")]
    public static void Init(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }
    
    internal static void Invoke(Func<Task> action)
    {
        if (_dispatcherQueue is null) // 未初始化（如单元测试等非App上下文）时降级为内联执行
        {
            _ = action();
            return;
        }
        _dispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    internal static void Invoke(Action action)
    {
        if (_dispatcherQueue is null)
        {
            action();
            return;
        }
        _dispatcherQueue.EnqueueAsync(action);
    }
}