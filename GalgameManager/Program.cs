using System.Runtime.InteropServices;
using GalgameManager.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace GalgameManager;

public static class BootstrapProgram
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        // Unpackaged 模式下必须先调用 Bootstrapper API 建立对 Windows App SDK Framework 包的动态依赖，
        if (!RuntimeHelper.IsMSIX)
        {
            try
            {
                // 版本取自所引用 Windows App SDK 包生成的常量，升级 SDK 时会自动跟随，不要硬编码
                Bootstrap.Initialize(Microsoft.WindowsAppSDK.Release.MajorMinor,
                    Microsoft.WindowsAppSDK.Release.VersionTag,
                    new PackageVersion(Microsoft.WindowsAppSDK.Runtime.Version.UInt64));
                AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                {
                    try
                    {
                        Bootstrap.Shutdown();
                    }
                    catch
                    {
                        // ignored
                    }
                };
            }
            catch (Exception ex)
            {
                var msg =
                    "未能初始化 Windows App SDK 运行时（免安装/便携模式需要安装 Windows App Runtime）。\n\n" +
                    $"错误：{ex.GetType().Name}: {ex.Message}\n\n" +
                    "请安装/修复 Windows App Runtime（x64）后再启动。";
                try
                {
                    MessageBoxW(IntPtr.Zero, msg, "PotatoVN 启动失败", 0);
                }
                catch
                {
                    // ignored
                }

#if DEBUG
                try
                {
                    var logPath = Path.Combine(AppContext.BaseDirectory, "winappsdk_bootstrap.log");
                    File.AppendAllText(logPath,
                        $"[{DateTimeOffset.Now:O}] Bootstrap.Initialize failed: {ex}{Environment.NewLine}");
                }
                catch
                {
                    // ignored
                }
#endif

                return;
            }
        }

        Application.Start((p) =>
        {
            DispatcherQueueSynchronizationContext context = new(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
