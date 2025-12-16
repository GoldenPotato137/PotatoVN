using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle; // 需要引入 AppLifecycle

namespace GalgameManager;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // 1. 获取当前实例信息
        var mainInstance = AppInstance.GetCurrent();
        var instances = AppInstance.GetInstances();

        // 2. 如果是多实例（热启动），直接放行
        // 因为 ActivationService 里的 RedirectActivationToAsync 会处理重定向，
        // 并且热启动的主调进程会自动退出，所以这里不需要我们干预。
        if (instances.Count > 1)
        {
            StartApp();
            return;
        }

        // 3. 如果是单实例（冷启动），检查是否需要“金蝉脱壳”
        // 我们定义一个特殊的参数 "/detached"，标记这是我们自己启动的 GUI 进程
        bool isInternalLaunch = args.Contains("/detached");

        if (!isInternalLaunch && IsCommandLineLaunch(args))
        {
            // --- 核心逻辑：启动替身，本体自杀 ---

            // 构建新的启动参数，加上 /detached 防止无限循环
            var escapedArgs = args.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg);
            var newArgs = string.Join(" ", escapedArgs) + " /detached";

            // 获取当前 exe 路径
            var exePath = Environment.ProcessPath;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = newArgs,
                UseShellExecute = true, // 关键：使用 ShellExecute 确保新进程与当前控制台无关联
                CreateNoWindow = true   // 不创建新窗口
            };

            try
            {
                Process.Start(startInfo);
                // 启动完替身，本体直接结束。
                // PowerShell 会收到进程结束信号，立即释放命令行。
                return;
            }
            catch (Exception)
            {
                // 如果启动失败，降级为普通启动，防止程序打不开
            }
        }

        // 4. 正常启动应用
        StartApp();
    }

    /// <summary>
    /// 判断是否是命令行别名启动（包含参数，且不是系统自动唤醒）
    /// </summary>
    private static bool IsCommandLineLaunch(string[] args)
    {
        // 如果没有参数，通常是点击开始菜单启动
        if (args.Length == 0) return false;

        // 通常 Alias 启动会带有参数（比如你的 UUID）
        // 或者你可以简单粗暴地认为：只要有参数，且不是 /detached，就认为是命令行启动
        return true;
    }

    private static void StartApp()
    {
        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
