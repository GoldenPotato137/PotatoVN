using GalgameManager.Core.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using Moq;
using Newtonsoft.Json;

namespace GalgameManager.Test.Services;

[TestFixture]
public class BgTaskServiceTest : ServiceTestBase
{
    private const string BgTaskFileName = "bgTasks.json";

    private FileService _fileService = null!;

    [SetUp]
    public void BgTaskServiceTestSetUp()
    {
        _fileService = new FileService();
        // bgTasks.json写在共享的AppStoragePaths.LocalDataPath下，每个用例开始前确保干净
        _fileService.Delete(AppStoragePaths.LocalDataPath, BgTaskFileName);
    }

    private BgTaskService CreateService() => new(InfoService.Object, _fileService, CreateServiceProvider());

    // 验证任务从添加到完成移除的完整生命周期：RunInternal被执行、BgTaskAdded/BgTaskRemoved按序触发、
    // 成功后通过IInfoService上报BgTaskSuccessEvent、任务列表最终清空
    [Test]
    public async Task AddBgTask_TaskRunsToCompletion_FullLifecycle()
    {
        BgTaskService service = CreateService();
        List<string> events = new();
        service.BgTaskAdded += _ => events.Add("added");
        service.BgTaskRemoved += _ => events.Add("removed");
        TestBgTask task = new();

        await service.AddBgTask(task);

        Assert.Multiple(() =>
        {
            Assert.That(task.Ran, Is.True);
            Assert.That(events, Is.EqualTo(new[] { "added", "removed" }));
            Assert.That(service.GetBgTasks(), Is.Empty);
        });
        InfoService.Verify(x => x.Event(EventType.BgTaskSuccessEvent, It.IsAny<InfoBarSeverity>(),
            It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<Action?>(),
            It.IsAny<string?>()), Times.Once);
    }

    // 验证任务执行抛异常时：异常不会向外抛出，而是转为BgTaskFailEvent事件上报，且任务仍被移出列表
    [Test]
    public async Task AddBgTask_TaskThrows_ReportsFailureAndRemovesTask()
    {
        BgTaskService service = CreateService();
        TestBgTask task = new() { ThrowOnRun = true };

        await service.AddBgTask(task);

        Assert.That(service.GetBgTasks(), Is.Empty);
        InfoService.Verify(x => x.Event(EventType.BgTaskFailEvent, It.IsAny<InfoBarSeverity>(),
            It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<string?>(), It.IsAny<Action?>(),
            It.IsAny<string?>()), Times.Once);
    }

    // 验证GetBgTask按任务类型+OnSearch关键字查找：类型与关键字都命中时返回实例本身，任一不符返回null
    [Test]
    public async Task GetBgTask_FiltersByTypeAndSearchKey()
    {
        BgTaskService service = CreateService();
        TestBgTask task = new() { SearchKey = "abc", Gate = new TaskCompletionSource() };
        Task addTask = service.AddBgTask(task); // 任务被Gate挡住，不会完成，会一直留在列表里

        Assert.Multiple(() =>
        {
            Assert.That(service.GetBgTask<TestBgTask>("abc"), Is.SameAs(task));
            Assert.That(service.GetBgTask<TestBgTask>("other"), Is.Null);
            Assert.That(service.GetBgTask<OtherTestBgTask>("abc"), Is.Null);
        });

        task.Gate.SetResult();
        await addTask;
    }

    // 验证后台任务的持久化与恢复闭环：SaveBgTasksString把运行中的任务写入文件，
    // 新实例ResolvedBgTasksAsync读回、反序列化、RecoverFromJson并重新运行，最后删除文件
    [Test]
    public async Task SaveAndResolve_RunningTask_RecoveredInNewServiceInstance()
    {
        BgTaskService service = CreateService();
        service.RegisterBgTaskType(typeof(TestBgTask), "-test");
        TestBgTask task = new() { Marker = "hello", Gate = new TaskCompletionSource() };
        Task addTask = service.AddBgTask(task);

        service.SaveBgTasksString();
        var file = Path.Combine(AppStoragePaths.LocalDataPath, BgTaskFileName);
        // FileService.Save走后台队列写入，且WaitForWriteFinishAsync只等队列出队、不等落盘，这里轮询等文件出现
        for (var i = 0; i < 50 && !File.Exists(file); i++) await Task.Delay(100);
        Assert.That(File.Exists(file), Is.True);

        task.Gate.SetResult();
        await addTask;

        BgTaskService service2 = CreateService(); // 模拟重启后的新实例
        service2.RegisterBgTaskType(typeof(TestBgTask), "-test");
        await service2.ResolvedBgTasksAsync();

        Assert.That(File.Exists(file), Is.False);
        TestBgTask? recovered = service2.GetBgTasks().OfType<TestBgTask>().FirstOrDefault();
        Assert.That(recovered, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(recovered!.Marker, Is.EqualTo("hello"));
            Assert.That(recovered.Recovered, Is.True);
        });
        // 等恢复的任务跑完并从列表移除，避免残留状态影响后续用例
        await Task.Delay(800);
        Assert.That(service2.GetBgTasks(), Is.Empty);
    }

    public class TestBgTask : BgTaskBase
    {
        public string? Marker { get; set; }

        [JsonIgnore] public bool Ran { get; private set; }

        [JsonIgnore] public bool Recovered { get; private set; }

        [JsonIgnore] public bool ThrowOnRun { get; set; }

        [JsonIgnore] public string? SearchKey { get; set; }

        [JsonIgnore] public TaskCompletionSource? Gate { get; set; }

        public override string Title => "TestBgTask";

        protected override Task RecoverFromJsonInternal()
        {
            Recovered = true;
            return Task.CompletedTask;
        }

        protected override async Task RunInternal()
        {
            Ran = true;
            if (ThrowOnRun) throw new InvalidOperationException("boom");
            if (Gate is not null) await Gate.Task;
            ChangeProgress(1, 1, "done");
        }

        public override bool OnSearch(string key) => key == SearchKey;
    }

    public class OtherTestBgTask : BgTaskBase
    {
        public override string Title => "OtherTestBgTask";

        protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

        protected override Task RunInternal() => Task.CompletedTask;
    }
}
