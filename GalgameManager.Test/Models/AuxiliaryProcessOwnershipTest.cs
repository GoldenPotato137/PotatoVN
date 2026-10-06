using System.Diagnostics;
using System.Reflection;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;

namespace GalgameManager.Test.Models;

[TestFixture]
public class AuxiliaryProcessOwnershipTest
{
    [Test]
    public async Task Mute_FirstPoll_OpensInitialTargetEvenWhenPidAlreadyKnown()
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        relay.Track(source);
        GameMuteTask task = new(new Galgame(), source, relay);
        typeof(GameMuteTask).GetMethod("TryFollowConfirmedProcess", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(task, null);
        Process? owned = GetProcess(task);
        Assert.That(owned, Is.Not.Null.And.Not.SameAs(source));
        Assert.That(owned!.Id, Is.EqualTo(source.Id));
        relay.Complete();
        task.Galgame = null;
        await InvokeRunInternalAsync(task);
        Assert.Throws<InvalidOperationException>(() => _ = owned.Id);
        Assert.That(source.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task Mapping_FirstPoll_OwnsTargetBeforeOwnerDisposesIt()
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        relay.Track(source);
        KeyMappingTask task = new(new Galgame(), source, [], null, relay);
        Task follow = (Task)typeof(KeyMappingTask)
            .GetMethod("FollowGameProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(task, null)!;
        Process? owned = GetProcess(task);
        Assert.That(owned, Is.Not.Null.And.Not.SameAs(source));
        relay.Complete();
        source.Dispose();
        await follow.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotThrow(() => _ = owned!.MainWindowHandle);
        task.Galgame = null;
        await InvokeRunInternalAsync(task);
        Assert.Throws<InvalidOperationException>(() => _ = owned!.Id);
    }

    [TestCase(typeof(KeyMappingTask))]
    [TestCase(typeof(GameMuteTask))]
    public async Task Recover_WithRelay_OwnsProcessUntilTaskCleanup(Type taskType)
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        int processId = source.Id;
        relay.Track(source);
        BgTaskBase task = CreateTask(taskType, source, relay);
        await task.RecoverFromJson();
        Process? owned = GetProcess(task);
        Assert.That(owned, Is.Not.Null.And.Not.SameAs(source));

        relay.Complete();
        source.Dispose();
        Assert.That(owned!.Id, Is.EqualTo(processId));
        Assert.DoesNotThrow(() => _ = owned.MainWindowHandle);

        SetGalgameNull(task);
        await InvokeRunInternalAsync(task);
        Assert.Throws<InvalidOperationException>(() => _ = owned.Id);
    }

    [Test]
    public async Task Magpie_AlreadyFinished_DisposesOwnConfirmationButNotSource()
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        relay.Track(source);
        CallMagpieTask task = new(new Galgame(), source, relay) { HashFinished = true };
        Process? owned = null;
        task.OnProgress += _ => owned = ((HashSet<Process>)typeof(CallMagpieTask)
            .GetField("_ownedProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(task)!).Single();
        Task run = InvokeRunInternalAsync(task);
        Assert.That(run.IsCompleted, Is.False);
        relay.Confirm(source);
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(owned, Is.Not.Null.And.Not.SameAs(source));
        Assert.Throws<InvalidOperationException>(() => _ = owned!.Id);
        Assert.That(source.Id, Is.GreaterThan(0));
        Assert.That(typeof(CallMagpieTask).GetField("_ownedProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(task), Is.Empty);
    }

    [Test]
    public async Task Magpie_CompletedWhileWaiting_DoesNotReadDisposedSource()
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        relay.Track(source);
        CallMagpieTask task = new(new Galgame(), source, relay);
        Task run = InvokeRunInternalAsync(task);
        Assert.That(run.IsCompleted, Is.False);
        relay.Complete();
        source.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(GetProcess(task), Is.Null);
    }

    [TestCase(typeof(KeyMappingTask))]
    [TestCase(typeof(GameMuteTask))]
    [TestCase(typeof(CallMagpieTask))]
    public async Task Constructor_WithRelay_DoesNotKeepBorrowedProcess(Type taskType)
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        relay.Track(source);
        BgTaskBase task = CreateTask(taskType, source, relay);
        Assert.That(GetProcess(task), Is.Null);
        relay.Complete();
        SetGalgameNull(task);
        if (task is CallMagpieTask)
            Assert.ThrowsAsync<PvnException>(() => InvokeRunInternalAsync(task));
        else
            await InvokeRunInternalAsync(task);
        Assert.That(source.Id, Is.GreaterThan(0));
    }

    [TestCase(typeof(KeyMappingTask))]
    [TestCase(typeof(GameMuteTask))]
    [TestCase(typeof(CallMagpieTask))]
    public async Task LegacyConstructor_CleanupDoesNotDisposeBorrowedProcess(Type taskType)
    {
        using Process source = Process.GetCurrentProcess();
        BgTaskBase task = CreateTask(taskType, source, null);
        Assert.That(GetProcess(task), Is.SameAs(source));
        SetGalgameNull(task);
        if (task is CallMagpieTask)
            Assert.ThrowsAsync<PvnException>(() => InvokeRunInternalAsync(task));
        else
            await InvokeRunInternalAsync(task);
        Assert.That(source.Id, Is.GreaterThan(0));
    }

    private static BgTaskBase CreateTask(Type type, Process source, GameRuntimeProcessRelay? relay)
    {
        Galgame game = new();
        if (type == typeof(KeyMappingTask)) return new KeyMappingTask(game, source, [], null, relay);
        if (type == typeof(GameMuteTask)) return new GameMuteTask(game, source, relay);
        return new CallMagpieTask(game, source, relay);
    }

    private static Process? GetProcess(BgTaskBase task) => (Process?)task.GetType()
        .GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(task);

    private static void SetGalgameNull(BgTaskBase task)
    {
        switch (task)
        {
            case KeyMappingTask mapping: mapping.Galgame = null; break;
            case GameMuteTask mute: mute.Galgame = null; break;
            case CallMagpieTask magpie: magpie.Galgame = null; break;
        }
    }

    private static Task InvokeRunInternalAsync(BgTaskBase task) => (Task)task.GetType()
        .GetMethod("RunInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(task, null)!;
}
