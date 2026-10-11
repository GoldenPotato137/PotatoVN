using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Globalization;
using Windows.System.UserProfile;
using GalgameManager.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;

namespace GalgameManager.Helpers;

/// <summary>
/// 崩溃现场诊断（见 issue #742）<br/>
/// 崩溃发生时立即把尽可能详细的现场写入日志：异常附带的 WinRT 信息、窗口与缩放、语言及 WinUI 的 MUI 资源、
/// 最近的操作轨迹；布局（Measure/Arrange）类崩溃还会逐层重新 Measure 可视树，找出具体抛异常的控件。
/// </summary>
public static class CrashDiagnostics
{
    private const int MaxBreadcrumbs = 30;
    private const int MaxDetailedReports = 3;
    private const int ProbeMaxElements = 20000;
    private const int MaxFrontierItems = 20;
    private const int MaxTextLength = 60;
    private const string XamlMuiFileName = "Microsoft.ui.xaml.dll.mui";
    private static readonly TimeSpan ProbeTimeBudget = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentQueue<string> Breadcrumbs = new();
    private static readonly DateTime ProcessStartTime = GetProcessStartTime();
    private static int _reportCount;

    private const string CrashLoopFallbackMsg =
        "PotatoVN 重启后再次崩溃，已停止自动重启。\nPotatoVN crashed again right after restarting, automatic restart has been stopped.\n\n" +
        "日志 / Logs:\n{0}\n\n是否打开日志文件夹？ / Open the log folder?";

    /// <summary>进程已运行的时长</summary>
    public static TimeSpan Uptime => DateTime.Now - ProcessStartTime;

    /// <summary>记录一条操作轨迹（页面跳转、窗口模式变化、弹出通知等），崩溃时一并写入报告</summary>
    public static void AddBreadcrumb(string message)
    {
        Breadcrumbs.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {message}");
        while (Breadcrumbs.Count > MaxBreadcrumbs && Breadcrumbs.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// 生成崩溃报告并写入日志，布局类异常额外探测出错的控件（每个进程只探测一次）<br/>
    /// 先写基础报告再探测：即使探测本身让进程直接退出，基础报告也已经落盘
    /// </summary>
    public static void WriteCrashReport(IInfoService infoService, Exception exception, string? message,
        UIElement? root)
    {
        var index = Interlocked.Increment(ref _reportCount);
        if (index > MaxDetailedReports)
        {
            infoService.Log(InfoBarSeverity.Error,
                $"[CrashReport #{index}] {exception.GetType().FullName} (0x{exception.HResult:X8}), details skipped");
            return;
        }

        infoService.Log(InfoBarSeverity.Error, BuildReport(exception, message, root, index));
        if (root is null || index > 1 || !IsLayoutException(exception)) return;
        string probe;
        try
        {
            probe = ProbeLayoutFailure(root);
        }
        catch (Exception e)
        {
            probe = $"  <probe failed: {e.GetType().Name} 0x{e.HResult:X8} {e.Message}>";
        }
        infoService.Log(InfoBarSeverity.Error, $"[CrashReport #{index}] Layout probe:\n{probe}");
    }

    /// <summary>
    /// 用 Win32 MessageBox（不依赖 XAML，XAML 本身可能已经坏了）告知用户应用连续崩溃、已停止自动重启，
    /// 并询问是否打开日志文件夹
    /// </summary>
    public static void ShowCrashLoopMessage(string logFolder)
    {
        try
        {
            var text = "App_CrashLoop_Msg".GetLocalized(logFolder);
            if (string.IsNullOrWhiteSpace(text) || text == "App_CrashLoop_Msg")
                text = string.Format(CrashLoopFallbackMsg, logFolder);
            var title = "App_CrashLoop_Title".GetLocalized();
            if (string.IsNullOrWhiteSpace(title) || title == "App_CrashLoop_Title")
                title = "PotatoVN";
            if (MessageBoxW(IntPtr.Zero, text, title, MbYesNo | MbIconError | MbSetForeground | MbTopMost) == IdYes)
                Process.Start(new ProcessStartInfo { FileName = logFolder, UseShellExecute = true });
        }
        catch (Exception)
        {
            // 尽力而为，弹窗失败也不能影响退出流程
        }
    }

    public static bool IsLayoutException(Exception exception)
    {
        var text = exception.ToString();
        return text.Contains("MeasureOverride") || text.Contains("ArrangeOverride");
    }

    #region Report

    private static string BuildReport(Exception exception, string? message, UIElement? root, int index)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[CrashReport #{index}] ===== PotatoVN crash report =====");
        AppendSection(sb, "Exception", s => AppendException(s, exception, message));
        AppendSection(sb, "App", AppendAppInfo);
        AppendSection(sb, "Window", s => AppendWindowInfo(s, root));
        AppendSection(sb, "Language", AppendLanguageInfo);
        AppendSection(sb, "WinUI MUI resources", AppendXamlMuiInfo);
        AppendSection(sb, "Accessibility", AppendAccessibilityInfo);
        AppendSection(sb, "Recent actions", s =>
        {
            foreach (var breadcrumb in Breadcrumbs.ToArray())
                s.AppendLine($"  {breadcrumb}");
        });
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, Action<StringBuilder> fill)
    {
        sb.AppendLine($"--- {title} ---");
        try
        {
            fill(sb);
        }
        catch (Exception e)
        {
            sb.AppendLine($"  <failed to collect: {e.GetType().Name} 0x{e.HResult:X8} {e.Message}>");
        }
    }

    /// <summary>单独捕获每一项，某一项取值失败不影响其他项</summary>
    private static void AppendLine(StringBuilder sb, string name, Func<object?> getter)
    {
        string value;
        try
        {
            value = getter() switch
            {
                null => "<null>",
                string s => s,
                IEnumerable<string> list => $"[{string.Join(", ", list)}]",
                var other => other.ToString() ?? string.Empty,
            };
        }
        catch (Exception e)
        {
            value = $"<error: {e.GetType().Name} 0x{e.HResult:X8}>";
        }
        sb.AppendLine($"  {name}: {value}");
    }

    private static void AppendException(StringBuilder sb, Exception exception, string? message)
    {
        if (!string.IsNullOrEmpty(message))
            sb.AppendLine($"  UnhandledException.Message: {message}");
        var depth = 0;
        for (Exception? e = exception; e is not null && depth < 5; e = e.InnerException, depth++)
        {
            sb.AppendLine($"  [{depth}] {e.GetType().FullName} HResult=0x{e.HResult:X8} ({DescribeHResult(e.HResult)})");
            if (!string.IsNullOrEmpty(e.Message))
                sb.AppendLine($"      Message: {e.Message}");
            // CsWinRT 会把 IRestrictedErrorInfo 的描述放在 Data 里（RestrictedDescription 等）
            foreach (DictionaryEntry entry in e.Data)
                sb.AppendLine($"      Data[{entry.Key}] = {entry.Value}");
        }
        sb.AppendLine("  Stack:");
        sb.AppendLine(exception.ToString());
    }

    private static string DescribeHResult(int hResult)
    {
        try
        {
            // FACILITY_WIN32：取低 16 位作为 Win32 错误码，例如 0x80073B01 = ERROR_MUI_FILE_NOT_LOADED
            if (((uint)hResult & 0xFFFF0000) == 0x80070000)
                return $"Win32 {hResult & 0xFFFF}: {new Win32Exception(hResult & 0xFFFF).Message}";
            return Marshal.GetExceptionForHR(hResult)?.Message ?? "unknown";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    private static void AppendAppInfo(StringBuilder sb)
    {
        AppendLine(sb, "Version", RuntimeHelper.GetVersion);
        AppendLine(sb, "Package", () => RuntimeHelper.IsMSIX ? Package.Current.Id.FullName : "<unpackaged>");
        AppendLine(sb, "IsStoreVersion", () => App.IsStoreVersion());
        AppendLine(sb, "IsPortable", () => AppStoragePaths.IsPortable);
        AppendLine(sb, "BaseDirectory", () => AppContext.BaseDirectory);
        AppendLine(sb, "WindowsAppRuntime", () => RuntimeInfo.AsString);
        AppendLine(sb, "WindowsAppSDK release", () => ReleaseInfo.AsString);
        AppendLine(sb, "OS", () => $"{RuntimeInformation.OSDescription} ({Environment.OSVersion.VersionString})");
        AppendLine(sb, "Architecture",
            () => $"process {RuntimeInformation.ProcessArchitecture}, OS {RuntimeInformation.OSArchitecture}");
        AppendLine(sb, ".NET", () => RuntimeInformation.FrameworkDescription);
        AppendLine(sb, "CommandLine", () => string.Join(' ', Environment.GetCommandLineArgs().Skip(1)));
        AppendLine(sb, "Uptime", () => Uptime.ToString(@"hh\:mm\:ss\.fff"));
        AppendLine(sb, "App.Status", () => App.Status);
    }

    private static void AppendWindowInfo(StringBuilder sb, UIElement? root)
    {
        AppendLine(sb, "MainWindow", () => App.MainWindow is { } window
            ? $"{window.AppWindow.Size.Width}x{window.AppWindow.Size.Height} at ({window.AppWindow.Position.X},{window.AppWindow.Position.Y})"
            : null);
        AppendLine(sb, "Root", () => root?.GetType().FullName);
        AppendLine(sb, "XamlRoot.Size", () => root?.XamlRoot?.Size);
        AppendLine(sb, "XamlRoot.RasterizationScale", () => root?.XamlRoot?.RasterizationScale);
        AppendLine(sb, "Current page", () => App.GetService<INavigationService>().Frame?.Content?.GetType().FullName);
        AppendLine(sb, "Open popups", () => root?.XamlRoot is { } xamlRoot
            ? VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot)
                .Select(p => p.Child?.GetType().FullName ?? "<empty>").ToList()
            : null);
    }

    private static void AppendLanguageInfo(StringBuilder sb)
    {
        AppendLine(sb, "CurrentUICulture", () => CultureInfo.CurrentUICulture.Name);
        AppendLine(sb, "CurrentCulture", () => CultureInfo.CurrentCulture.Name);
        AppendLine(sb, "PrimaryLanguageOverride", () => ApplicationLanguages.PrimaryLanguageOverride);
        AppendLine(sb, "ApplicationLanguages", () => ApplicationLanguages.Languages.ToList());
        AppendLine(sb, "ManifestLanguages", () => ApplicationLanguages.ManifestLanguages.ToList());
        AppendLine(sb, "GlobalizationPreferences", () => GlobalizationPreferences.Languages.ToList());
        AppendLine(sb, "UserDefaultUILanguage", () =>
        {
            var langId = GetUserDefaultUILanguage();
            return $"{CultureInfo.GetCultureInfo(langId).Name} (0x{langId:X4})";
        });
        AppendLine(sb, "UserPreferredUILanguages", () => GetUiLanguages(GetUserPreferredUILanguages, MuiLanguageName));
        AppendLine(sb, "ProcessPreferredUILanguages",
            () => GetUiLanguages(GetProcessPreferredUILanguages, MuiLanguageName));
        AppendLine(sb, "ThreadPreferredUILanguages (with fallback)",
            () => GetUiLanguages(GetThreadPreferredUILanguages, MuiLanguageName | MuiMergeUserFallback | MuiMergeSystemFallback));
    }

    /// <summary>
    /// 0x80073B01 是 ERROR_MUI_FILE_NOT_LOADED，记录 WinUI 实际加载的 Microsoft.UI.Xaml.dll 位置，
    /// 以及当前首选界面语言是否都有对应的 .mui 资源
    /// </summary>
    private static void AppendXamlMuiInfo(StringBuilder sb)
    {
        var xamlDll = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .FirstOrDefault(m => string.Equals(m.ModuleName, "Microsoft.UI.Xaml.dll", StringComparison.OrdinalIgnoreCase))
            ?.FileName;
        AppendLine(sb, "Microsoft.UI.Xaml.dll", () => xamlDll);
        if (xamlDll is null || Path.GetDirectoryName(xamlDll) is not { } dir) return;
        AppendLine(sb, "Language folders with .mui", () => Directory.GetDirectories(dir)
            .Where(d => File.Exists(Path.Combine(d, XamlMuiFileName)))
            .Select(Path.GetFileName).OfType<string>().ToList());
        IEnumerable<string> languages = GetUiLanguages(GetThreadPreferredUILanguages,
            MuiLanguageName | MuiMergeUserFallback | MuiMergeSystemFallback);
        foreach (var language in languages)
            AppendLine(sb, $"  .mui for {language}", () => File.Exists(Path.Combine(dir, language, XamlMuiFileName)));
    }

    private static void AppendAccessibilityInfo(StringBuilder sb)
    {
        AppendLine(sb, "ScreenReader flag", () =>
        {
            var enabled = 0;
            return SystemParametersInfoW(SpiGetScreenReader, 0, ref enabled, 0) ? enabled != 0 : null;
        });
        // 有 UIA 客户端（读屏、手柄映射、各类辅助/叠加工具）在监听时，控件会额外创建 AutomationPeer 并发送通知
        AppendLine(sb, "UIA listener AutomationFocusChanged",
            () => AutomationPeer.ListenerExists(AutomationEvents.AutomationFocusChanged));
        AppendLine(sb, "UIA listener PropertyChanged",
            () => AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged));
        AppendLine(sb, "UIA listener StructureChanged",
            () => AutomationPeer.ListenerExists(AutomationEvents.StructureChanged));
        AppendLine(sb, "UIA listener LiveRegionChanged",
            () => AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged));
    }

    #endregion

    #region Layout probe

    /// <summary>
    /// 从根节点开始重新 Measure，失败就往下找第一个同样失败的子元素，直到找到最内层抛异常的元素。<br/>
    /// 原始异常的调用栈只经过托管元素（Page/UserControl），具体是哪个 WinUI 控件出错需要这样逐层还原
    /// </summary>
    private static string ProbeLayoutFailure(UIElement root)
    {
        ProbeContext context = new(root);
        StringBuilder sb = new();
        List<UIElement> candidates = [root];
        try
        {
            if (root.XamlRoot is { } xamlRoot)
                candidates.AddRange(VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot)
                    .Select(p => p.Child).OfType<UIElement>());
        }
        catch (Exception)
        {
            // 拿不到弹出层就只探测主树
        }

        foreach (UIElement candidate in candidates)
        {
            if (context.TryMeasure(candidate, out _) && context.TryMeasure(candidate, out _, deep: true)) continue;
            List<UIElement> path = [];
            (UIElement culprit, Exception error) = FindDeepestFailure(context, candidate, path);
            sb.AppendLine($"  Reproduced: re-measure threw {error.GetType().Name} HResult=0x{error.HResult:X8}");
            sb.AppendLine("  Failing path (outermost -> innermost):");
            for (var i = 0; i < path.Count; i++)
                sb.AppendLine($"  [{i}] {Describe(path[i])}");
            sb.AppendLine($"  >>> Innermost failing element: {Describe(culprit)}");
            sb.AppendLine($"  >>> Its visual children: {SafeChildrenCount(culprit)}");
            sb.AppendLine($"  Probe: {context.Visited} measures, {context.Elapsed.TotalMilliseconds:0} ms");
            return sb.ToString();
        }

        sb.AppendLine($"  Not reproduced: re-measuring {context.Visited} elements did not throw.");
        sb.AppendLine("  Where layout stopped (measured but never arranged / template never applied):");
        List<string> frontier = [];
        CollectLayoutFrontier(root, frontier, 0);
        foreach (var item in frontier)
            sb.AppendLine($"  - {item}");
        if (frontier.Count == 0)
            sb.AppendLine("  <none>");
        return sb.ToString();
    }

    private static (UIElement, Exception) FindDeepestFailure(ProbeContext context, UIElement start, List<UIElement> path)
    {
        UIElement current = start;
        Exception error = context.LastError!;
        path.Add(current);
        // InvalidateMeasure 只标脏元素本身，没变脏的子元素会直接返回缓存结果，到不了出错的控件；
        // 浅层测量找不到出错的子元素时，改为先把子元素整棵子树标脏再测量
        var deep = false;
        while (!context.Exhausted)
        {
            UIElement? failingChild = FindFailingChild(context, current, deep, ref error);
            if (failingChild is null && !deep)
            {
                deep = true;
                failingChild = FindFailingChild(context, current, deep, ref error);
            }
            if (failingChild is null) break;
            current = failingChild;
            path.Add(current);
        }
        return (current, error);
    }

    private static UIElement? FindFailingChild(ProbeContext context, UIElement parent, bool deep, ref Exception error)
    {
        foreach (UIElement child in GetVisibleChildren(parent))
        {
            if (context.Exhausted) break;
            if (context.TryMeasure(child, out Exception? childError, deep)) continue;
            error = childError!;
            return child;
        }
        return null;
    }

    private static void CollectLayoutFrontier(UIElement element, List<string> result, int depth)
    {
        if (result.Count >= MaxFrontierItems || depth > 256) return;
        foreach (UIElement child in GetVisibleChildren(element))
        {
            if (result.Count >= MaxFrontierItems) return;
            var neverArranged = child.ActualSize is { X: 0, Y: 0 } &&
                                (child.DesiredSize.Width > 0 || child.DesiredSize.Height > 0);
            var templateNotApplied = child is Control && SafeChildrenCount(child) == 0;
            if (neverArranged || templateNotApplied)
            {
                result.Add($"{(neverArranged ? "[not arranged] " : "[no template] ")}{Describe(child)}");
                continue;
            }
            CollectLayoutFrontier(child, result, depth + 1);
        }
    }

    private static List<UIElement> GetVisibleChildren(DependencyObject parent)
    {
        List<UIElement> result = [];
        try
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
                if (VisualTreeHelper.GetChild(parent, i) is UIElement { Visibility: Visibility.Visible } child)
                    result.Add(child);
        }
        catch (Exception)
        {
            // 返回已经拿到的部分
        }
        return result;
    }

    private static int SafeChildrenCount(DependencyObject element)
    {
        try
        {
            return VisualTreeHelper.GetChildrenCount(element);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static string Describe(DependencyObject element)
    {
        try
        {
            StringBuilder sb = new(element.GetType().FullName);
            if (element is FrameworkElement fe)
            {
                if (!string.IsNullOrEmpty(fe.Name)) sb.Append($" Name='{fe.Name}'");
                var automationId = AutomationProperties.GetAutomationId(fe);
                if (!string.IsNullOrEmpty(automationId)) sb.Append($" AutomationId='{automationId}'");
                sb.Append($" Actual={fe.ActualWidth:0.#}x{fe.ActualHeight:0.#}");
                sb.Append($" Desired={fe.DesiredSize.Width:0.#}x{fe.DesiredSize.Height:0.#}");
                if (fe.DataContext is { } dataContext) sb.Append($" DataContext={dataContext.GetType().Name}");
            }
            switch (element)
            {
                case TextBlock textBlock:
                    sb.Append($" Text='{Truncate(textBlock.Text)}' Font='{textBlock.FontFamily?.Source}'");
                    break;
                case FontIcon fontIcon:
                    sb.Append($" Glyph={DescribeGlyph(fontIcon.Glyph)} Font='{fontIcon.FontFamily?.Source}'");
                    break;
                case InfoBar infoBar:
                    sb.Append($" IsOpen={infoBar.IsOpen} Severity={infoBar.Severity}" +
                              $" IsIconVisible={infoBar.IsIconVisible} IsClosable={infoBar.IsClosable}");
                    break;
                case BitmapIcon bitmapIcon:
                    sb.Append($" UriSource='{bitmapIcon.UriSource}'");
                    break;
                case Image { Source: BitmapImage bitmap }:
                    sb.Append($" UriSource='{bitmap.UriSource}'");
                    break;
                case ContentControl { Content: string content }:
                    sb.Append($" Content='{Truncate(content)}'");
                    break;
            }
            return sb.ToString();
        }
        catch (Exception e)
        {
            return $"{element.GetType().FullName} <describe failed: 0x{e.HResult:X8}>";
        }
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.ReplaceLineEndings(" ");
        return text.Length <= MaxTextLength ? text : text[..MaxTextLength] + "...";
    }

    private static string DescribeGlyph(string? glyph) =>
        string.IsNullOrEmpty(glyph) ? "<empty>" : string.Join(' ', glyph.Select(c => $"U+{(int)c:X4}"));

    private sealed class ProbeContext(UIElement root)
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Size _fallbackSize = GetFallbackSize(root);

        public int Visited { get; private set; }
        public Exception? LastError { get; private set; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;
        public bool Exhausted => Visited >= ProbeMaxElements || _stopwatch.Elapsed > ProbeTimeBudget;

        /// <summary>
        /// 先 InvalidateMeasure 再 Measure，强制重新走一遍该元素的 Measure<br/>
        /// deep 为 true 时把整棵子树都标脏，保证子树里每个元素都会重新 Measure
        /// </summary>
        public bool TryMeasure(UIElement element, out Exception? error, bool deep = false)
        {
            Visited++;
            error = null;
            try
            {
                Size size = LayoutInformation.GetAvailableSize(element);
                if (!(size.Width > 0) || !(size.Height > 0)) size = _fallbackSize;
                if (deep)
                    InvalidateSubtree(element);
                else
                    element.InvalidateMeasure();
                element.Measure(size);
                return true;
            }
            catch (Exception e)
            {
                error = LastError = e;
                return false;
            }
        }

        private void InvalidateSubtree(UIElement root)
        {
            Stack<UIElement> stack = new();
            stack.Push(root);
            var count = 0;
            while (stack.Count > 0 && count++ < ProbeMaxElements && !Exhausted)
            {
                UIElement element = stack.Pop();
                element.InvalidateMeasure();
                foreach (UIElement child in GetVisibleChildren(element))
                    stack.Push(child);
            }
        }

        private static Size GetFallbackSize(UIElement root)
        {
            try
            {
                if (root.ActualSize is { X: > 0, Y: > 0 } actual) return new Size(actual.X, actual.Y);
                if (root.XamlRoot?.Size is { Width: > 0, Height: > 0 } xamlRootSize) return xamlRootSize;
            }
            catch (Exception)
            {
                // 使用默认值
            }
            return new Size(1920, 1080);
        }
    }

    #endregion

    #region Native

    private const uint MuiLanguageName = 0x8;
    private const uint MuiMergeSystemFallback = 0x10;
    private const uint MuiMergeUserFallback = 0x20;
    private const uint SpiGetScreenReader = 0x0046;
    private const uint MbYesNo = 0x4;
    private const uint MbIconError = 0x10;
    private const uint MbSetForeground = 0x10000;
    private const uint MbTopMost = 0x40000;
    private const int IdYes = 6;

    private delegate bool GetUiLanguagesFunc(uint flags, out uint count, char[]? buffer, ref uint bufferLength);

    private static List<string> GetUiLanguages(GetUiLanguagesFunc func, uint flags)
    {
        uint bufferLength = 0;
        if (!func(flags, out _, null, ref bufferLength) || bufferLength == 0) return [];
        var buffer = new char[bufferLength];
        if (!func(flags, out _, buffer, ref bufferLength)) return [];
        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static DateTime GetProcessStartTime()
    {
        try
        {
            return Process.GetCurrentProcess().StartTime;
        }
        catch (Exception)
        {
            return DateTime.Now;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserPreferredUILanguages(uint dwFlags, out uint pulNumLanguages,
        char[]? pwszLanguagesBuffer, ref uint pcchLanguagesBuffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetProcessPreferredUILanguages(uint dwFlags, out uint pulNumLanguages,
        char[]? pwszLanguagesBuffer, ref uint pcchLanguagesBuffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetThreadPreferredUILanguages(uint dwFlags, out uint pulNumLanguages,
        char[]? pwszLanguagesBuffer, ref uint pcchLanguagesBuffer);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    #endregion
}
