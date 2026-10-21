using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public class AutoExportServiceTest : ServiceTestBase
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private FixedTimeProvider _timeProvider = null!;

    [SetUp]
    public void AutoExportServiceTestSetUp()
    {
        _timeProvider = new FixedTimeProvider(Now);
    }

    // 验证关闭开关会真实写入设置，而不是只改变当前页面上的显示状态
    [Test]
    public async Task SetEnabledAsync_Disable_PersistsFalse()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoExport, true);
        TestAutoExportService service = CreateService();

        bool result = await service.SetEnabledAsync(false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(Settings.ReadSettingAsync<bool>(KeyValues.AutoExport).Result, Is.False);
        });
    }

    // 验证导出路径无效时不能启用自动导出，并清除之前残留的启用状态
    [Test]
    public async Task SetEnabledAsync_InvalidPath_PersistsFalse()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoExport, true);
        await Settings.SaveSettingAsync(KeyValues.AutoExportPath, Path.Combine(TestDir, "不存在"));
        TestAutoExportService service = CreateService();

        bool result = await service.SetEnabledAsync(true);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(Settings.ReadSettingAsync<bool>(KeyValues.AutoExport).Result, Is.False);
        });
    }

    // 验证应用启动后会立即检查一次；到达间隔时无需再次启动应用即可导出
    [Test]
    public async Task Start_ExportIsDue_EnqueuesImmediately()
    {
        string exportPath = await ConfigureAsync(enabled: true, lastExportTime: Now.AddHours(-2));
        var addedCount = 0;
        BgTaskService.Setup(x => x.AddBgTask(It.IsAny<BgTaskBase>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref addedCount);
                await Settings.SaveSettingAsync(KeyValues.LastExportTime, Now);
            });
        TestAutoExportService service = CreateService();

        service.Start();
        service.Start();
        await WaitUntilAsync(() => Volatile.Read(ref addedCount) == 1, "等待自动导出任务入队超时");
        service.Stop();

        Assert.That(service.CreatedPaths, Is.EqualTo(new[] { exportPath }));
    }

    // 验证未到间隔时启动调度不会提前导出
    [Test]
    public async Task Start_ExportIsNotDue_DoesNotEnqueue()
    {
        await ConfigureAsync(enabled: true, lastExportTime: Now.AddMinutes(-30));
        TestAutoExportService service = CreateService();

        service.Start();
        await Task.Delay(200);
        service.Stop();

        BgTaskService.Verify(x => x.AddBgTask(It.IsAny<BgTaskBase>()), Times.Never);
    }

    // 验证软件运行期间开启自动导出会唤醒调度，不需要重启应用
    [Test]
    public async Task SettingChanged_EnableWhileRunning_EnqueuesWithoutRestart()
    {
        await ConfigureAsync(enabled: false, lastExportTime: Now.AddHours(-2));
        var addedCount = 0;
        BgTaskService.Setup(x => x.AddBgTask(It.IsAny<BgTaskBase>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref addedCount);
                await Settings.SaveSettingAsync(KeyValues.LastExportTime, Now);
            });
        TestAutoExportService service = CreateService();
        service.Start();
        await Task.Delay(100);

        await Settings.SaveSettingAsync(KeyValues.AutoExport, true);
        Settings.RaiseSettingChanged(KeyValues.AutoExport, true);

        await WaitUntilAsync(() => Volatile.Read(ref addedCount) == 1, "运行中开启后未触发自动导出");
        service.Stop();
    }

    // 验证自动与手动导出共用同一把锁，不会同时创建两个导出任务
    [Test]
    public async Task ExportAsync_ConcurrentCalls_OnlyStartsOneTask()
    {
        string exportPath = CreateDir("Export");
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var addedCount = 0;
        BgTaskService.Setup(x => x.AddBgTask(It.IsAny<BgTaskBase>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref addedCount);
                return gate.Task;
            });
        TestAutoExportService service = CreateService();

        Task<bool> firstExport = service.ExportAsync(exportPath);
        await WaitUntilAsync(() => Volatile.Read(ref addedCount) == 1, "第一个导出任务未入队");
        bool secondResult = await service.ExportAsync(exportPath);
        gate.SetResult();
        bool firstResult = await firstExport;

        Assert.Multiple(() =>
        {
            Assert.That(firstResult, Is.True);
            Assert.That(secondResult, Is.False);
            Assert.That(addedCount, Is.EqualTo(1));
        });
    }

    // 验证导出任务失败（导出时间未更新）后停止调度并提示，本次运行内不再重试；下次启动时恢复
    [Test]
    public async Task Start_ExportFails_StopsUntilNextStart()
    {
        await ConfigureAsync(enabled: true, lastExportTime: Now.AddHours(-2));
        var addedCount = 0;
        var exportSucceeds = false;
        BgTaskService.Setup(x => x.AddBgTask(It.IsAny<BgTaskBase>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref addedCount);
                if (Volatile.Read(ref exportSucceeds))
                    await Settings.SaveSettingAsync(KeyValues.LastExportTime, Now);
            });
        TestAutoExportService service = CreateService();

        service.Start();
        await WaitUntilAsync(() => Volatile.Read(ref addedCount) == 1, "第一次自动导出未触发");
        Settings.RaiseSettingChanged(KeyValues.AutoExportInterval, 1d);
        await Task.Delay(200);
        int addedCountAfterFailure = Volatile.Read(ref addedCount);

        Volatile.Write(ref exportSucceeds, true);
        service.Start(); // 模拟下次启动软件
        await WaitUntilAsync(() => Volatile.Read(ref addedCount) == 2, "重新启动后未恢复自动导出");
        service.Stop();

        Assert.That(addedCountAfterFailure, Is.EqualTo(1));
        VerifyPausedNotified(Times.Once());
    }

    // 验证调度过程中抛出异常时同样停止调度并提示
    [Test]
    public async Task Start_ExportThrows_StopsAndNotifies()
    {
        await ConfigureAsync(enabled: true, lastExportTime: Now.AddHours(-2));
        BgTaskService.Setup(x => x.AddBgTask(It.IsAny<BgTaskBase>()))
            .ThrowsAsync(new IOException("测试异常"));
        TestAutoExportService service = CreateService();

        service.Start();
        await WaitUntilAsync(() => InfoService.Invocations.Count > 0, "导出异常后未提示");
        Settings.RaiseSettingChanged(KeyValues.AutoExportInterval, 1d);
        await Task.Delay(200);
        service.Stop();

        BgTaskService.Verify(x => x.AddBgTask(It.IsAny<BgTaskBase>()), Times.Once);
        VerifyPausedNotified(Times.Once(), withException: true);
    }

    // 验证达到备份数量上限时，会在新导出开始前删除最旧的文件并保留较新的备份
    [Test]
    public async Task Start_BackupLimitReached_PrunesOldestFiles()
    {
        string exportPath = await ConfigureAsync(enabled: true, lastExportTime: Now.AddHours(-2));
        await Settings.SaveSettingAsync(KeyValues.MaxBackupNumber, 2);
        string oldest = Path.Combine(exportPath, "PotatoVN_oldest.pvnExport.zip");
        string middle = Path.Combine(exportPath, "PotatoVN_middle.pvnExport.zip");
        string newest = Path.Combine(exportPath, "PotatoVN_newest.pvnExport.zip");
        await File.WriteAllTextAsync(oldest, "oldest");
        await File.WriteAllTextAsync(middle, "middle");
        await File.WriteAllTextAsync(newest, "newest");
        File.SetCreationTimeUtc(oldest, Now.AddDays(-3));
        File.SetCreationTimeUtc(middle, Now.AddDays(-2));
        File.SetCreationTimeUtc(newest, Now.AddDays(-1));
        var addedCount = 0;
        BgTaskService.Setup(x => x.AddBgTask(It.IsAny<BgTaskBase>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref addedCount);
                await Settings.SaveSettingAsync(KeyValues.LastExportTime, Now);
            });
        TestAutoExportService service = CreateService();

        service.Start();
        await WaitUntilAsync(() => Volatile.Read(ref addedCount) == 1, "等待滚动备份后的导出任务入队超时");
        service.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(oldest), Is.False);
            Assert.That(File.Exists(middle), Is.False);
            Assert.That(File.Exists(newest), Is.True);
        });
    }

    private TestAutoExportService CreateService() =>
        new(Settings, BgTaskService.Object, InfoService.Object, _timeProvider);

    private void VerifyPausedNotified(Times times, bool withException = false) =>
        InfoService.Verify(x => x.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
            It.IsAny<string>(), It.Is<Exception?>(e => (e != null) == withException), It.IsAny<string?>(),
            It.IsAny<Action?>(), It.IsAny<string?>()), times);

    private async Task<string> ConfigureAsync(bool enabled, DateTime lastExportTime)
    {
        string exportPath = CreateDir("Export");
        await Settings.SaveSettingAsync(KeyValues.AutoExport, enabled);
        await Settings.SaveSettingAsync(KeyValues.AutoExportPath, exportPath);
        await Settings.SaveSettingAsync(KeyValues.AutoExportInterval, 1d);
        await Settings.SaveSettingAsync(KeyValues.LastExportTime, lastExportTime);
        await Settings.SaveSettingAsync(KeyValues.MaxBackupNumber, 5);
        return exportPath;
    }

    private sealed class TestAutoExportService(ILocalSettingsService localSettingsService,
        IBgTaskService bgTaskService, IInfoService infoService, TimeProvider timeProvider)
        : AutoExportService(localSettingsService, bgTaskService, infoService, timeProvider)
    {
        public List<string> CreatedPaths { get; } = [];

        protected override BgTaskBase CreateExportTask(string targetPath)
        {
            CreatedPaths.Add(targetPath);
            return new TestExportTask();
        }
    }

    private sealed class TestExportTask : BgTaskBase
    {
        public override string Title => "测试导出任务";

        protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

        protected override Task RunInternal() => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
