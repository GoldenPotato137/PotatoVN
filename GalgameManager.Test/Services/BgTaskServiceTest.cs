using GalgameManager.Contracts.Services;
using GalgameManager.Core.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using Microsoft.Extensions.DependencyInjection;
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

    private BgTaskService CreateService()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILocalSettingsService>(Settings);
        services.AddSingleton(GalgameCollectionService.Object);
        return new BgTaskService(InfoService.Object, _fileService, services.BuildServiceProvider());
    }

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

    [Test]
    public async Task AddBgTask_ConcurrentPlayTimeDuplicates_OnlyOneRuns()
    {
        BgTaskService service = CreateService();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid gameId = Guid.NewGuid();
        TestRecordPlayTimeTask[] tasks = Enumerable.Range(0, 32)
            .Select(_ => new TestRecordPlayTimeTask(Settings, GalgameCollectionService.Object)
            {
                GameId = gameId,
                Gate = gate,
            })
            .ToArray();
        var addedCount = 0;
        var attemptedCount = 0;
        service.BgTaskAdded += _ => Interlocked.Increment(ref addedCount);

        Task[] additions = tasks.Select(task => Task.Run(async () =>
        {
            await start.Task;
            Task completion = service.AddBgTask(task);
            Interlocked.Increment(ref attemptedCount);
            await completion;
        })).ToArray();
        start.SetResult();

        try
        {
            await WaitUntilAsync(() => Volatile.Read(ref attemptedCount) == tasks.Length,
                "并发添加未全部完成检查");
            Assert.Multiple(() =>
            {
                Assert.That(service.GetBgTasks().Count(), Is.EqualTo(1));
                Assert.That(tasks.Sum(task => task.RunCount), Is.EqualTo(1));
                Assert.That(addedCount, Is.EqualTo(1));
                Assert.That(service.GetBgTask<RecordPlayTimeTask>(gameId.ToString("D")), Is.Not.Null);
            });
        }
        finally
        {
            gate.SetResult();
            await Task.WhenAll(additions);
        }
    }

    [Test]
    public async Task AddBgTask_DuplicateAfterCompletion_RunsAgain()
    {
        BgTaskService service = CreateService();
        Guid gameId = Guid.NewGuid();
        TestRecordPlayTimeTask first = new(Settings, GalgameCollectionService.Object) { GameId = gameId };
        TestRecordPlayTimeTask second = new(Settings, GalgameCollectionService.Object) { GameId = gameId };

        await service.AddBgTask(first);
        await service.AddBgTask(second);

        Assert.Multiple(() =>
        {
            Assert.That(first.RunCount, Is.EqualTo(1));
            Assert.That(second.RunCount, Is.EqualTo(1));
            Assert.That(service.GetBgTasks(), Is.Empty);
        });
    }

    [Test]
    public async Task AddBgTask_DifferentGames_RunTogether()
    {
        BgTaskService service = CreateService();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestRecordPlayTimeTask first = new(Settings, GalgameCollectionService.Object)
        {
            GameId = Guid.NewGuid(), Gate = gate,
        };
        TestRecordPlayTimeTask second = new(Settings, GalgameCollectionService.Object)
        {
            GameId = Guid.NewGuid(), Gate = gate,
        };

        Task[] additions = [service.AddBgTask(first), service.AddBgTask(second)];
        try
        {
            Assert.That(service.GetBgTasks().Count(), Is.EqualTo(2));
        }
        finally
        {
            gate.SetResult();
            await Task.WhenAll(additions);
        }
    }

    [Test]
    public async Task AddBgTask_SameGameDifferentInstallations_OnlyOneRuns()
    {
        BgTaskService service = CreateService();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Galgame game = new();
        TestRecordPlayTimeTask first = new(Settings, GalgameCollectionService.Object)
        {
            Galgame = game, InstallationId = Guid.NewGuid(), Gate = gate,
        };
        TestRecordPlayTimeTask second = new(Settings, GalgameCollectionService.Object)
        {
            Galgame = game, InstallationId = Guid.NewGuid(), Gate = gate,
        };
        Task completion = service.AddBgTask(first);
        try
        {
            await service.AddBgTask(second);
            Assert.Multiple(() =>
            {
                Assert.That(first.RunCount, Is.EqualTo(1));
                Assert.That(second.RunCount, Is.Zero);
                Assert.That(service.GetBgTasks().Count(), Is.EqualTo(1));
            });
        }
        finally
        {
            gate.SetResult();
            await completion;
        }
    }

    [Test]
    public async Task AddBgTask_PlayTimeTaskFails_CanRunAgain()
    {
        BgTaskService service = CreateService();
        Guid gameId = Guid.NewGuid();
        TestRecordPlayTimeTask first = new(Settings, GalgameCollectionService.Object)
        {
            GameId = gameId, ThrowOnRun = true,
        };
        TestRecordPlayTimeTask second = new(Settings, GalgameCollectionService.Object) { GameId = gameId };
        await service.AddBgTask(first);
        await service.AddBgTask(second);
        Assert.Multiple(() =>
        {
            Assert.That(first.Task.IsFaulted, Is.True);
            Assert.That(second.RunCount, Is.EqualTo(1));
            Assert.That(service.GetBgTasks(), Is.Empty);
        });
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

    [Test]
    public async Task Resolve_DuplicatePersistedTasks_OnlyOneIsRestored()
    {
        BgTaskService service = CreateService();
        service.RegisterBgTaskType(typeof(TestRecordPlayTimeTask), "-record-test");
        TestRecordPlayTimeTask.RecoveryGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid gameId = Guid.NewGuid();
        string json = JsonConvert.SerializeObject(new
        {
            GameId = gameId,
            DeduplicationKey = gameId.ToString("D"), // 兼容此前已持久化的冗余字段。
        });
        string persisted = $"-record-test {json.ToBase64()} -record-test {json.ToBase64()} ";
        _fileService.Save(AppStoragePaths.LocalDataPath, BgTaskFileName, persisted);
        string file = Path.Combine(AppStoragePaths.LocalDataPath, BgTaskFileName);
        for (var i = 0; i < 50 && !File.Exists(file); i++) await Task.Delay(100);

        try
        {
            await service.ResolvedBgTasksAsync();
            TestRecordPlayTimeTask[] recovered = service.GetBgTasks().OfType<TestRecordPlayTimeTask>().ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(recovered, Has.Length.EqualTo(1));
                Assert.That(recovered.Sum(task => task.RunCount), Is.EqualTo(1));
                Assert.That(service.GetBgTask<RecordPlayTimeTask>(gameId.ToString("D")), Is.Not.Null);
                Assert.That(File.Exists(file), Is.False);
            });
        }
        finally
        {
            TestRecordPlayTimeTask.RecoveryGate.SetResult();
        }
        await Task.Delay(800);
        Assert.That(service.GetBgTasks(), Is.Empty);
    }

    [Test]
    public async Task AddBgTask_NonPlayTimeTasksWithSameSearchKey_AreNotDeduplicated()
    {
        BgTaskService service = CreateService();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestBgTask first = new() { SearchKey = "same", Gate = gate };
        TestBgTask second = new() { SearchKey = "same", Gate = gate };
        Task[] additions = [service.AddBgTask(first), service.AddBgTask(second)];
        try
        {
            Assert.That(service.GetBgTasks().Count(), Is.EqualTo(2));
        }
        finally
        {
            gate.SetResult();
            await Task.WhenAll(additions);
        }
    }

    [Test]
    public async Task Resolve_ExistingPlayTimeTask_DoesNotStartAnother()
    {
        BgTaskService service = CreateService();
        service.RegisterBgTaskType(typeof(TestRecordPlayTimeTask), "-record-test");
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestRecordPlayTimeTask active = new(Settings, GalgameCollectionService.Object)
        {
            GameId = Guid.NewGuid(), Gate = gate,
        };
        Task completion = service.AddBgTask(active);
        string json = JsonConvert.SerializeObject(new { active.GameId });
        _fileService.Save(AppStoragePaths.LocalDataPath, BgTaskFileName, $"-record-test {json.ToBase64()} ");
        string file = Path.Combine(AppStoragePaths.LocalDataPath, BgTaskFileName);
        try
        {
            await WaitUntilAsync(() => File.Exists(file), "恢复文件未写入");
            await service.ResolvedBgTasksAsync();
            Assert.That(service.GetBgTasks(), Is.EqualTo(new[] { active }));
        }
        finally
        {
            gate.SetResult();
            await completion;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RecordPlayTimeTask_OnSearch_UsesLogicalGameId(bool uppercase)
    {
        Galgame game = new();
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object) { Galgame = game };
        string key = game.Uuid.ToString("D");
        if (uppercase) key = key.ToUpperInvariant();
        Assert.Multiple(() =>
        {
            Assert.That(task.OnSearch(key), Is.True);
            Assert.That(task.OnSearch(Guid.NewGuid().ToString("D")), Is.False);
            Assert.That(task.OnSearch(string.Empty), Is.False);
        });
        task.Galgame = null;
        Assert.That(task.OnSearch(key), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AddBgTask_AuxiliaryTask_OnlySharesSameGameSession(bool differentGame)
    {
        BgTaskService service = CreateService();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestRecordPlayTimeTask record = new(Settings, GalgameCollectionService.Object)
        {
            GameId = Guid.NewGuid(), Gate = gate,
        };
        TestKeyMappingTask mapping = new()
        {
            GameId = differentGame ? Guid.NewGuid() : record.GameId, Gate = gate,
        };
        Task recordTask = service.AddBgTask(record);
        Task mappingTask = service.AddBgTask(mapping);
        try
        {
            Assert.That(mapping.ConnectedRelay is not null, Is.EqualTo(!differentGame));
            if (!differentGame)
                Assert.That(mapping.ConnectedRelay, Is.SameAs(GetRecordRelay(record)));
        }
        finally
        {
            gate.SetResult();
            await Task.WhenAll(recordTask, mappingTask);
        }
    }

    [Test]
    public async Task Resolve_AuxiliarySavedFirst_ConnectsAfterRecordRecovery()
    {
        BgTaskService service = CreateService();
        service.RegisterBgTaskType(typeof(TestRecordPlayTimeTask), "-record-test");
        service.RegisterBgTaskType(typeof(TestKeyMappingTask), "-key-test");
        service.RegisterBgTaskType(typeof(TestGameMuteTask), "-mute-test");
        service.RegisterBgTaskType(typeof(TestCallMagpieTask), "-magpie-test");
        TestRecordPlayTimeTask.RecoveryGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestKeyMappingTask.RecoveryGate = TestRecordPlayTimeTask.RecoveryGate;
        string json = JsonConvert.SerializeObject(new { GameId = Guid.NewGuid() });
        _fileService.Save(AppStoragePaths.LocalDataPath, BgTaskFileName,
            $"-key-test {json.ToBase64()} -mute-test {json.ToBase64()} -magpie-test {json.ToBase64()} " +
            $"-record-test {json.ToBase64()} ");
        try
        {
            await WaitUntilAsync(() => File.Exists(Path.Combine(AppStoragePaths.LocalDataPath, BgTaskFileName)),
                "恢复文件未写入");
            await service.ResolvedBgTasksAsync();
            TestRecordPlayTimeTask record = service.GetBgTasks().OfType<TestRecordPlayTimeTask>().Single();
            TestKeyMappingTask mapping = service.GetBgTasks().OfType<TestKeyMappingTask>().Single();
            Assert.That(mapping.ConnectedRelay, Is.SameAs(GetRecordRelay(record)));
            Assert.That(GetAuxiliaryRelay(typeof(GameMuteTask), service.GetBgTasks().OfType<TestGameMuteTask>().Single()),
                Is.SameAs(GetRecordRelay(record)));
            Assert.That(GetAuxiliaryRelay(typeof(CallMagpieTask), service.GetBgTasks().OfType<TestCallMagpieTask>().Single()),
                Is.SameAs(GetRecordRelay(record)));
        }
        finally
        {
            TestRecordPlayTimeTask.RecoveryGate.SetResult();
        }
        await WaitUntilAsync(() => !service.GetBgTasks().Any(), "恢复任务未结束");
    }

    private static object? GetRecordRelay(RecordPlayTimeTask task) => typeof(RecordPlayTimeTask)
        .GetProperty("ProcessRelay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(task);

    private static object? GetAuxiliaryRelay(Type type, object task) => type
        .GetField("_processRelay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(task);

    public class TestKeyMappingTask : KeyMappingTask
    {
        public static TaskCompletionSource? RecoveryGate { get; set; }
        public Guid GameId
        {
            get => Galgame?.Uuid ?? Guid.Empty;
            set => Galgame = new Galgame { Uuid = value };
        }
        [JsonIgnore] public TaskCompletionSource? Gate { get; set; }
        [JsonIgnore] public object? ConnectedRelay => typeof(KeyMappingTask)
            .GetField("_processRelay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(this);
        protected override async Task RecoverFromJsonInternal()
        {
            Gate = RecoveryGate;
            await base.RecoverFromJsonInternal();
        }
        protected override async Task RunInternal()
        {
            if (Gate is not null) await Gate.Task;
        }
    }

    public class TestGameMuteTask : GameMuteTask
    {
        public Guid GameId
        {
            get => Galgame?.Uuid ?? Guid.Empty;
            set => Galgame = new Galgame { Uuid = value };
        }
        protected override Task RunInternal() => TestKeyMappingTask.RecoveryGate!.Task;
    }

    public class TestCallMagpieTask : CallMagpieTask
    {
        public Guid GameId
        {
            get => Galgame?.Uuid ?? Guid.Empty;
            set => Galgame = new Galgame { Uuid = value };
        }
        protected override Task RunInternal() => TestKeyMappingTask.RecoveryGate!.Task;
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

    // 只替换运行和恢复中的平台交互，保留计时任务本身的游戏标识与OnSearch规则。
    public class TestRecordPlayTimeTask : RecordPlayTimeTask
    {
        public static TaskCompletionSource? RecoveryGate { get; set; }

        public TestRecordPlayTimeTask(ILocalSettingsService settings, IGalgameCollectionService gameService)
            : base(settings, gameService)
        {
        }

        public Guid GameId
        {
            get => Galgame?.Uuid ?? Guid.Empty;
            set => Galgame = new Galgame { Uuid = value };
        }

        [JsonIgnore] public TaskCompletionSource? Gate { get; set; }
        [JsonIgnore] public int RunCount;
        [JsonIgnore] public bool ThrowOnRun { get; set; }
        public override bool ProgressOnTrayIcon => false;

        protected override Task RecoverFromJsonInternal()
        {
            Gate = RecoveryGate;
            return Task.CompletedTask;
        }

        protected override async Task RunInternal()
        {
            Interlocked.Increment(ref RunCount);
            if (ThrowOnRun) throw new InvalidOperationException("boom");
            if (Gate is not null) await Gate.Task;
        }
    }
}
