using System.Diagnostics;
using System.Runtime.InteropServices;
using GalgameManager.Test.Upgrade;

namespace GalgameManager.Test.Lifecycle;

[TestFixture]
[NonParallelizable]
[Category("E2E")]
public sealed class UnpackagedAppSingleInstanceTest
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RedirectTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);
    private const int StackOverflowExitCode = unchecked((int)0xC00000FD);
    private const int SwMinimize = 6;

    private readonly List<Process> _processes = [];
    private string _runRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _runRoot = Path.Combine(Path.GetTempPath(), "PotatoVN.E2E", "SingleInstance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_runRoot, "LocalData"));
        Directory.CreateDirectory(Path.Combine(_runRoot, "Temp"));
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (Process process in _processes)
        {
            await StopProcessAsync(process);
            process.Dispose();
        }
        _processes.Clear();
        TryDeleteDirectory(_runRoot);
    }

    /// <summary>
    /// 免安装版第二实例曾在解析服务时撞上被第一实例独占的设置数据库，进而在异常处理中递归栈溢出
    /// </summary>
    [Test]
    public async Task SecondInstance_ShouldRedirectActivationToFirstInstanceAndExit()
    {
        // Arrange
        Process first = StartApp();
        IntPtr window = await WaitForMainWindowAsync(first);
        Assert.That(window, Is.Not.EqualTo(IntPtr.Zero), "第一实例主窗口未在启动超时内出现");
        await Task.Delay(TimeSpan.FromSeconds(2)); // 等启动流程走完，避免其自身的窗口操作干扰下方断言
        NativeMethods.ShowWindow(window, SwMinimize);
        Assert.That(await WaitUntilAsync(() => NativeMethods.IsIconic(window), ExitTimeout), Is.True,
            "无法最小化第一实例主窗口");

        // Act
        Process second = StartApp();
        var exited = await WaitForExitAsync(second, RedirectTimeout);

        // Assert
        Assert.That(exited, Is.True, $"第二实例未在 {RedirectTimeout.TotalSeconds}s 内退出");
        Assert.That(second.ExitCode, Is.Not.EqualTo(StackOverflowExitCode), "第二实例因栈溢出崩溃");
        Assert.That(second.ExitCode, Is.EqualTo(0), "第二实例应在转交激活后正常退出");
        Assert.That(first.HasExited, Is.False, "第一实例应继续运行");
        Assert.That(await WaitUntilAsync(() => !NativeMethods.IsIconic(window), RedirectTimeout), Is.True,
            "第一实例应响应转交来的激活并还原主窗口");
    }

    private Process StartApp()
    {
        var exePath = GetExePath();
        Assert.That(File.Exists(exePath), Is.True, $"未找到客户端 exe: {exePath}");

        ProcessStartInfo startInfo = new()
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            Environment =
            {
                ["POTATOVN_LOCALDATA_PATH"] = Path.Combine(_runRoot, "LocalData"),
                ["POTATOVN_TEMP_PATH"] = Path.Combine(_runRoot, "Temp"),
                ["POTATOVN_PORTABLE"] = "0",
                ["POTATOVN_UPGRADE_UI_TEST"] = "1",
            },
        };
        Process process = Process.Start(startInfo) ?? throw new AssertionException("启动进程失败");
        _processes.Add(process);
        return process;
    }

    private static string GetExePath()
    {
        var configuration = AppContext.BaseDirectory.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
        return Path.Combine(UpgradeTestData.WorkspaceRoot, "GalgameManager", "bin", "x64", configuration,
            "net8.0-windows10.0.22621.0", "win-x64", "GalgameManager.exe");
    }

    private static async Task<IntPtr> WaitForMainWindowAsync(Process process)
    {
        IntPtr window = IntPtr.Zero;
        await WaitUntilAsync(() =>
        {
            if (process.HasExited) return true;
            process.Refresh();
            window = process.MainWindowHandle;
            return window != IntPtr.Zero;
        }, StartupTimeout);
        return window;
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition()) return true;
            await Task.Delay(200);
        }
        return condition();
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using CancellationTokenSource cancellationTokenSource = new(timeout);
        try
        {
            await process.WaitForExitAsync(cancellationTokenSource.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task StopProcessAsync(Process process)
    {
        try
        {
            if (process.HasExited) return;
            process.CloseMainWindow();
            if (await WaitForExitAsync(process, ExitTimeout)) return;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            // 进程已在检查和终止之间退出。
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 测试结果不应被临时目录清理失败覆盖。
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr window);
    }
}
