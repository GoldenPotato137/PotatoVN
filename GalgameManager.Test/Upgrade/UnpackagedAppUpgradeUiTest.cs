using System.Diagnostics;
using System.Runtime.InteropServices;
using GalgameManager.Enums;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Test.Upgrade;

[TestFixture]
[NonParallelizable]
[Category("E2E")]
public sealed class UnpackagedAppUpgradeUiTest
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan UiCommandTimeout = TimeSpan.FromSeconds(10);

    [TestCase("1.7.2")]
    [TestCase("1.8.0")]
    public async Task UnpackagedApp_ShouldDisplayMigratedData(string version)
    {
        var workspaceRoot = UpgradeTestData.WorkspaceRoot;
        var configuration = InferConfigurationFromTestOutput();
        var exePath = Path.Combine(
            workspaceRoot,
            "GalgameManager",
            "bin",
            "x64",
            configuration,
            "net8.0-windows10.0.22621.0",
            "win-x64",
            "GalgameManager.exe");

        Assert.That(File.Exists(exePath), Is.True, $"未找到客户端 exe: {exePath}");

        var fixtureDir = UpgradeTestData.GetFixtureDir(version);
        Assert.That(Directory.Exists(fixtureDir), Is.True, $"未找到旧数据夹具目录: {fixtureDir}");

        IReadOnlyList<string> expectedGalgameNames = UpgradeTestData.ReadExpectedGalgameNames(fixtureDir);
        IReadOnlyList<ExpectedCategoryGroup> expectedCategoryGroups =
            UpgradeTestData.ReadExpectedCategoryGroups(fixtureDir);
        IReadOnlyList<string> expectedSourceNames = UpgradeTestData.ReadExpectedGalgameSourceNames(fixtureDir);
        Assert.That(expectedGalgameNames, Is.Not.Empty, $"夹具版本 {version} 应至少包含一个游戏");
        Assert.That(expectedCategoryGroups, Is.Not.Empty, $"夹具版本 {version} 应至少包含一个分类组");
        Assert.That(expectedSourceNames, Is.Not.Empty, $"夹具版本 {version} 应至少包含一个游戏来源");

        var runRoot = Path.Combine(Path.GetTempPath(), "PotatoVN.E2E", version, Guid.NewGuid().ToString("N"));
        var localDataPath = Path.Combine(runRoot, "LocalData");
        var tempPath = Path.Combine(runRoot, "Temp");
        Directory.CreateDirectory(localDataPath);
        Directory.CreateDirectory(tempPath);
        UpgradeTestData.CopyJsonOnly(fixtureDir, localDataPath);

        ProcessStartInfo startInfo = new()
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            Environment =
            {
                ["POTATOVN_LOCALDATA_PATH"] = localDataPath,
                ["POTATOVN_TEMP_PATH"] = tempPath,
                ["POTATOVN_PORTABLE"] = "0",
                ["POTATOVN_UPGRADE_UI_TEST"] = "1",
            },
        };

        using Process process = Process.Start(startInfo) ?? throw new AssertionException("启动进程失败");
        var app = process.Id.ToString();
        var closeRequested = false;

        try
        {
            await RunRequiredWinAppUiAsync(
                StartupTimeout + UiCommandTimeout,
                $"[{version}] 应用主界面未在升级完成后出现",
                "wait-for", "AppNavigation", "--app", app,
                "--timeout", ((int)StartupTimeout.TotalMilliseconds).ToString(), "--json");

            await NavigateAsync(app, "HomeNavItem", "HomeGameGrid", version);
            await AssertHomeDataAsync(app, expectedGalgameNames, version);

            await NavigateAsync(app, "CategoryNavItem", "CategoryGroupNavigation", version);
            await AssertCategoryDataAsync(app, expectedCategoryGroups, version);

            await NavigateAsync(app, "LibraryNavItem", "LibraryContent", version);
            await AssertLibraryDataAsync(app, expectedSourceNames, version);

            await BringToForegroundAsync(process, version);
            await RunRequiredWinAppUiAsync(
                UiCommandTimeout,
                $"[{version}] 无法通过 Alt+F4 关闭应用",
                "send-keys", "alt+f4", "--app", app,
                "--via", "send-input", "--allow-system-keys", "--json");
            closeRequested = true;

            var exited = await WaitForExitAsync(process, UiCommandTimeout);
            Assert.That(exited, Is.True, $"[{version}] 应用收到 Alt+F4 后未在 {UiCommandTimeout.TotalSeconds}s 内退出");
            Assert.That(process.ExitCode, Is.EqualTo(0), $"[{version}] 应用应正常退出");
        }
        finally
        {
            if (!process.HasExited && !closeRequested)
            {
                try
                {
                    TryBringToForeground(process);
                    await Task.Delay(200);
                    await RunWinAppUiAsync(
                        UiCommandTimeout,
                        "send-keys", "alt+f4", "--app", app,
                        "--via", "send-input", "--allow-system-keys", "--json");
                }
                catch
                {
                    // 主断言失败时，仍会在下方强制回收测试进程。
                }
            }

            await StopProcessAsync(process, UiCommandTimeout);
            TryDeleteDirectory(runRoot);
        }
    }

    private static async Task BringToForegroundAsync(Process process, string version)
    {
        var activated = TryBringToForeground(process);
        await Task.Delay(200);
        Assert.That(activated && NativeMethods.GetForegroundWindow() == process.MainWindowHandle, Is.True,
            $"[{version}] 无法将应用窗口切换到前台以安全发送 Alt+F4");
    }

    private static bool TryBringToForeground(Process process)
    {
        process.Refresh();
        var targetWindow = process.MainWindowHandle;
        if (targetWindow == IntPtr.Zero) return false;

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
        var currentThread = NativeMethods.GetCurrentThreadId();
        var attached = foregroundThread != currentThread &&
                       NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            NativeMethods.ShowWindow(targetWindow, 9);
            NativeMethods.BringWindowToTop(targetWindow);
            return NativeMethods.SetForegroundWindow(targetWindow);
        }
        finally
        {
            if (attached) NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    private static async Task NavigateAsync(string app, string navigationItem, string pageRoot, string version)
    {
        await RunRequiredWinAppUiAsync(
            UiCommandTimeout,
            $"[{version}] 无法调用导航项 {navigationItem}",
            "invoke", navigationItem, "--app", app, "--json");
        await RunRequiredWinAppUiAsync(
            UiCommandTimeout + TimeSpan.FromSeconds(5),
            $"[{version}] 导航后未出现页面 {pageRoot}",
            "wait-for", pageRoot, "--app", app,
            "--timeout", ((int)UiCommandTimeout.TotalMilliseconds).ToString(), "--json");
    }

    private static async Task AssertHomeDataAsync(
        string app,
        IReadOnlyList<string> expectedNames,
        string version)
    {
        IReadOnlyList<UiElementSnapshot> actualGames = await WaitForUiElementsAsync(
            app,
            "HomeGameGrid",
            "HomeGame_",
            items => items.Count >= expectedNames.Count && ContainsExpectedNames(items, expectedNames),
            version,
            "首页游戏");

        Assert.That(actualGames, Has.Count.EqualTo(expectedNames.Count),
            $"[{version}] 首页游戏数量应与升级前一致");
        Assert.That(actualGames.Select(item => item.Name), Is.EquivalentTo(expectedNames),
            $"[{version}] 首页游戏名称应与升级前一致");
    }

    private static async Task AssertCategoryDataAsync(
        string app,
        IReadOnlyList<ExpectedCategoryGroup> expectedGroups,
        string version)
    {
        IReadOnlyList<UiElementSnapshot> actualGroups = await WaitForUiElementsAsync(
            app,
            "CategoryGroupNavigation",
            "CategoryGroup_",
            items => items.Count >= expectedGroups.Count &&
                     expectedGroups.All(expected => FindCategoryGroup(items, expected) is not null),
            version,
            "分类组");

        Assert.That(actualGroups.Count, Is.GreaterThanOrEqualTo(expectedGroups.Count),
            $"[{version}] 升级后分类组数量不应少于升级前");
        Assert.That(actualGroups.Any(group => group.AutomationId.StartsWith(
                "CategoryGroup_1_", StringComparison.Ordinal)), Is.True,
            $"[{version}] 升级后应存在 Status 分类组(Type=1)");

        foreach (ExpectedCategoryGroup expectedGroup in expectedGroups)
        {
            UiElementSnapshot? actualGroup = FindCategoryGroup(actualGroups, expectedGroup);
            Assert.That(actualGroup, Is.Not.Null,
                $"[{version}] 升级后应保留分类组 {expectedGroup.DisplayName}");
        }

        HashSet<string> actualCategoryIds = new(StringComparer.Ordinal);
        foreach (UiElementSnapshot actualGroup in actualGroups)
        {
            var groupId = ReadTrailingGuid(actualGroup.AutomationId, "CategoryGroup_");
            Assert.That(groupId, Is.Not.Null,
                $"[{version}] 分类组缺少有效的 UI 标识: {actualGroup.AutomationId}");

            await RunRequiredWinAppUiAsync(
                UiCommandTimeout,
                $"[{version}] 无法选择分类组 {actualGroup.Name}",
                "invoke", actualGroup.AutomationId, "--app", app, "--json");

            var categoryRoot = $"CategoryItems_{groupId:N}";
            await RunRequiredWinAppUiAsync(
                UiCommandTimeout + TimeSpan.FromSeconds(5),
                $"[{version}] 分类组 {actualGroup.Name} 的内容未加载",
                "wait-for", categoryRoot, "--app", app,
                "--timeout", ((int)UiCommandTimeout.TotalMilliseconds).ToString(), "--json");

            ExpectedCategoryGroup? expectedGroup = expectedGroups.FirstOrDefault(
                expected => MatchesCategoryGroup(actualGroup, expected));
            IReadOnlyList<UiElementSnapshot> actualCategories = await WaitForUiElementsAsync(
                app,
                categoryRoot,
                "Category_",
                items => expectedGroup is null || expectedGroup.Categories.All(
                    expected => FindCategory(items, expected) is not null),
                version,
                $"分类组 {actualGroup.Name} 中的分类");

            foreach (UiElementSnapshot category in actualCategories)
                actualCategoryIds.Add(category.AutomationId);

            if (expectedGroup is null) continue;
            Assert.That(actualCategories.Count, Is.GreaterThanOrEqualTo(expectedGroup.Categories.Count),
                $"[{version}] 分类组 {expectedGroup.DisplayName} 中的分类数量不应少于升级前");
            foreach (ExpectedCategory expectedCategory in expectedGroup.Categories)
            {
                UiElementSnapshot? actualCategory = FindCategory(actualCategories, expectedCategory);
                Assert.That(actualCategory, Is.Not.Null,
                    $"[{version}] 升级后应保留分类 {expectedCategory.Name}");
                Assert.That(actualCategory!.ContainsDescendantName($"×{expectedCategory.GameCount}"), Is.True,
                    $"[{version}] 分类 {expectedCategory.Name} 的游戏数量应为 {expectedCategory.GameCount}");
            }
        }

        var expectedCategoryCount = expectedGroups.Sum(group => group.Categories.Count);
        Assert.That(actualCategoryIds.Count, Is.GreaterThanOrEqualTo(expectedCategoryCount),
            $"[{version}] 升级后分类总数不应少于升级前");
    }

    private static async Task AssertLibraryDataAsync(
        string app,
        IReadOnlyList<string> expectedNames,
        string version)
    {
        IReadOnlyList<UiElementSnapshot> actualSources = await WaitForUiElementsAsync(
            app,
            "LibraryContent",
            "LibrarySource_",
            items => items.Count >= expectedNames.Count && ContainsExpectedNames(items, expectedNames),
            version,
            "游戏来源");

        Assert.That(actualSources.Count, Is.GreaterThanOrEqualTo(expectedNames.Count),
            $"[{version}] 升级后游戏来源数量不应少于升级前");
        AssertContainsExpectedNames(actualSources, expectedNames, version, "游戏来源");
    }

    private static async Task<IReadOnlyList<UiElementSnapshot>> WaitForUiElementsAsync(
        string app,
        string root,
        string automationIdPrefix,
        Func<IReadOnlyList<UiElementSnapshot>, bool> isReady,
        string version,
        string dataType)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        WinAppUiCommandResult? lastResult = null;
        IReadOnlyList<UiElementSnapshot> lastItems = [];

        while (stopwatch.Elapsed < UiCommandTimeout)
        {
            lastResult = await RunWinAppUiAsync(
                UiCommandTimeout,
                "inspect", root, "--app", app, "--depth", "10", "--json");
            if (lastResult.ExitCode == 0)
            {
                try
                {
                    JToken tree = JToken.Parse(lastResult.StandardOutput);
                    lastItems = ReadUiElements(tree, automationIdPrefix);
                    if (isReady(lastItems)) return lastItems;
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    // UIA tree may change during the page transition; retry until the page settles.
                }
            }

            await Task.Delay(250);
        }

        Assert.Fail(
            $"[{version}] UI 中的{dataType}不符合预期。已找到: " +
            $"{string.Join(", ", lastItems.Select(item => item.Name))}。" +
            (lastResult?.FormatOutput() ?? "未取得 UIA 树"));
        return [];
    }

    private static IReadOnlyList<UiElementSnapshot> ReadUiElements(JToken tree, string automationIdPrefix)
    {
        return tree.SelectTokens("$..automationId")
            .Select(token => token.Parent?.Parent)
            .OfType<JObject>()
            .Where(element => element["automationId"]?.Value<string>()?.StartsWith(
                automationIdPrefix, StringComparison.Ordinal) is true)
            .Select(element => new UiElementSnapshot(
                element["automationId"]!.Value<string>()!,
                element["name"]?.Value<string>() ?? string.Empty,
                element))
            .DistinctBy(item => item.AutomationId)
            .ToList();
    }

    private static UiElementSnapshot? FindCategoryGroup(
        IEnumerable<UiElementSnapshot> actualGroups,
        ExpectedCategoryGroup expectedGroup)
    {
        return actualGroups.FirstOrDefault(actual => MatchesCategoryGroup(actual, expectedGroup));
    }

    private static bool MatchesCategoryGroup(UiElementSnapshot actual, ExpectedCategoryGroup expected)
    {
        var prefix = $"CategoryGroup_{expected.Type}_";
        if (!actual.AutomationId.StartsWith(prefix, StringComparison.Ordinal)) return false;
        if (expected.Id is { } id)
            return actual.AutomationId.Equals($"{prefix}{id:N}", StringComparison.Ordinal);

        return expected.Type != (int)CategoryGroupType.Custom ||
               actual.Name.Equals(expected.DisplayName, StringComparison.Ordinal);
    }

    private static UiElementSnapshot? FindCategory(
        IEnumerable<UiElementSnapshot> actualCategories,
        ExpectedCategory expectedCategory)
    {
        return expectedCategory.Id is { } id
            ? actualCategories.FirstOrDefault(actual => actual.AutomationId.Equals(
                $"Category_{id:N}", StringComparison.Ordinal))
            : actualCategories.FirstOrDefault(actual => actual.Name.Equals(
                expectedCategory.Name, StringComparison.Ordinal));
    }

    private static Guid? ReadTrailingGuid(string automationId, string expectedPrefix)
    {
        if (!automationId.StartsWith(expectedPrefix, StringComparison.Ordinal)) return null;
        var separator = automationId.LastIndexOf('_');
        return separator >= 0 && Guid.TryParseExact(automationId[(separator + 1)..], "N", out Guid id)
            ? id
            : null;
    }

    private static bool ContainsExpectedNames(
        IReadOnlyList<UiElementSnapshot> actualItems,
        IReadOnlyList<string> expectedNames)
    {
        List<string> remaining = actualItems.Select(item => item.Name).ToList();
        foreach (string expectedName in expectedNames)
        {
            var index = remaining.FindIndex(name => name.Equals(expectedName, StringComparison.Ordinal));
            if (index < 0) return false;
            remaining.RemoveAt(index);
        }
        return true;
    }

    private static void AssertContainsExpectedNames(
        IReadOnlyList<UiElementSnapshot> actualItems,
        IReadOnlyList<string> expectedNames,
        string version,
        string dataType)
    {
        Assert.That(ContainsExpectedNames(actualItems, expectedNames), Is.True,
            $"[{version}] 升级后应保留全部{dataType}。期望: {string.Join(", ", expectedNames)}；" +
            $"实际: {string.Join(", ", actualItems.Select(item => item.Name))}");
    }

    private static async Task RunRequiredWinAppUiAsync(
        TimeSpan timeout,
        string failureMessage,
        params string[] arguments)
    {
        WinAppUiCommandResult result = await RunWinAppUiAsync(timeout, arguments);
        Assert.That(result.ExitCode, Is.EqualTo(0), $"{failureMessage}。{result.FormatOutput()}");
    }

    private static async Task<WinAppUiCommandResult> RunWinAppUiAsync(TimeSpan timeout, params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "winapp",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("ui");
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception e)
        {
            throw new AssertionException(
                $"无法启动 WinApp CLI。请先执行 winget install --id Microsoft.WinAppCLI。错误：{e.Message}");
        }

        if (process is null) throw new AssertionException("无法启动 WinApp CLI 进程");
        using (process)
        {
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource cancellationTokenSource = new(timeout);
            try
            {
                await process.WaitForExitAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
                catch (InvalidOperationException)
                {
                    // 进程已在超时和终止之间退出。
                }

                throw new AssertionException($"WinApp CLI 在 {timeout.TotalSeconds}s 内未完成");
            }

            return new WinAppUiCommandResult(process.ExitCode, await standardOutput, await standardError);
        }
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

    private static async Task StopProcessAsync(Process process, TimeSpan gracefulTimeout)
    {
        if (process.HasExited || await WaitForExitAsync(process, gracefulTimeout)) return;
        try
        {
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

    private static string InferConfigurationFromTestOutput()
    {
        var baseDir = AppContext.BaseDirectory;
        return baseDir.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
    }

    private sealed record WinAppUiCommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string FormatOutput() =>
            $"ExitCode={ExitCode}\nstdout:\n{StandardOutput}\nstderr:\n{StandardError}";
    }

    private sealed record UiElementSnapshot(string AutomationId, string Name, JObject Element)
    {
        public bool ContainsDescendantName(string name)
        {
            return Element.SelectTokens("$..name")
                .Any(token => token.Value<string>()?.Equals(name, StringComparison.Ordinal) is true);
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr window);
    }

}
