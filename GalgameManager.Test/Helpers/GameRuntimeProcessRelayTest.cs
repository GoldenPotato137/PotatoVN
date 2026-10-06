using System.Diagnostics;
using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class GameRuntimeProcessRelayTest
{
    [Test]
    public async Task Confirm_PublishesConfirmedProcess()
    {
        GameRuntimeProcessRelay relay = new();
        using Process process = Process.GetCurrentProcess();

        relay.Confirm(process);
        using Process? confirmed = await relay.WaitForConfirmationAsync();

        Assert.Multiple(() =>
        {
            Assert.That(confirmed, Is.Not.SameAs(process));
            Assert.That(confirmed?.Id, Is.EqualTo(process.Id));
            Assert.That(relay.IsCompleted, Is.False);
        });
    }

    [Test]
    public async Task CompleteWithoutConfirmation_ReleasesWaiterWithNull()
    {
        GameRuntimeProcessRelay relay = new();
        Task<Process?> waiter = relay.WaitForConfirmationAsync();

        relay.Complete();
        Process? result = await waiter;

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(relay.IsCompleted, Is.True);
        });
    }

    [Test]
    public async Task ConfirmAfterCompletion_DoesNotPublishProcess()
    {
        GameRuntimeProcessRelay relay = new();
        using Process process = Process.GetCurrentProcess();
        relay.Complete();

        relay.Confirm(process);

        Assert.That(await relay.WaitForConfirmationAsync(), Is.Null);
        Assert.That(relay.OpenCurrentProcess(), Is.Null);
    }

    [Test]
    public async Task Track_OnlyChangesTarget_DoesNotStartGameplay()
    {
        GameRuntimeProcessRelay relay = new();
        using Process process = Process.GetCurrentProcess();
        relay.Track(process);
        Task<Process?> confirmation = relay.WaitForConfirmationAsync();
        using Process? observed = relay.OpenCurrentProcess();
        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.Not.SameAs(process));
            Assert.That(observed?.Id, Is.EqualTo(process.Id));
            Assert.That(confirmation.IsCompleted, Is.False);
            Assert.That(relay.Completion.IsCompleted, Is.False);
        });
        relay.Complete();
        Assert.That(await confirmation, Is.Null);
        Assert.That(relay.Completion.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task Track_AfterConfirmation_WaitsForNewTargetConfirmation()
    {
        GameRuntimeProcessRelay relay = new();
        using Process first = Process.GetCurrentProcess();
        using Process second = Process.GetCurrentProcess();
        relay.Confirm(first);
        relay.Track(second);
        Task<Process?> confirmation = relay.WaitForConfirmationAsync();
        Assert.That(confirmation.IsCompleted, Is.False);
        relay.Confirm(second);
        using Process? confirmed = await confirmation;
        Assert.That(confirmed?.Id, Is.EqualTo(second.Id));
        Assert.That(confirmed, Is.Not.SameAs(second));
    }

    [Test]
    public async Task Complete_AfterConfirmation_ReleasesAllConsumersAndClearsTarget()
    {
        GameRuntimeProcessRelay relay = new();
        using Process process = Process.GetCurrentProcess();
        relay.Confirm(process);
        Task[] consumers = Enumerable.Range(0, 16).Select(_ => relay.Completion).ToArray();
        relay.Complete();
        relay.Track(process);
        relay.Confirm(process);
        await Task.WhenAll(consumers).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Multiple(() =>
        {
            Assert.That(relay.OpenCurrentProcess(), Is.Null);
            Assert.That(relay.IsCompleted, Is.True);
        });
        Assert.That(await relay.WaitForConfirmationAsync(), Is.Null);
    }

    [Test]
    public async Task Complete_ConcurrentWithTrackAndConfirm_RemainsTerminal()
    {
        GameRuntimeProcessRelay relay = new();
        using Process process = Process.GetCurrentProcess();
        await Task.WhenAll(Task.Run(() => relay.Track(process)), Task.Run(() => relay.Confirm(process)),
            Task.Run(relay.Complete));
        Assert.Multiple(() =>
        {
            Assert.That(relay.IsCompleted, Is.True);
            Assert.That(relay.OpenCurrentProcess(), Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ConsumerReadingWindow_AfterOwnerCompletionAndDisposal_RemainsValid(bool waitForConfirmation)
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        int processId = source.Id;
        relay.Track(source);
        TaskCompletionSource acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource ownerDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task consumer = Task.Run(async () =>
        {
            using Process? owned = waitForConfirmation
                ? await relay.WaitForConfirmationAsync()
                : relay.OpenCurrentProcess();
            Assert.That(owned, Is.Not.Null);
            acquired.SetResult();
            await ownerDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(owned!.Id, Is.EqualTo(processId));
            Assert.DoesNotThrow(() => _ = owned.MainWindowHandle);
        });
        try
        {
            if (waitForConfirmation) relay.Confirm(source);
            await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
            relay.Complete();
            source.Dispose();
        }
        finally
        {
            ownerDisposed.TrySetResult();
        }
        await consumer.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(relay.OpenCurrentProcess(), Is.Null);
        Assert.That(await relay.WaitForConfirmationAsync(), Is.Null);
    }

    [Test]
    public void OpenCurrentProcess_UnchangedTarget_DoesNotOpenAnotherObject()
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        relay.Track(source);
        using Process? owned = relay.OpenCurrentProcess();
        Assert.That(owned, Is.Not.Null);
        Assert.That(relay.OpenCurrentProcess(owned!.Id), Is.Null);
        Assert.That(source.Id, Is.EqualTo(owned.Id));
    }

    [Test]
    public async Task Confirm_MultipleConsumers_OwnSeparateObjects()
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = Process.GetCurrentProcess();
        relay.Confirm(source);
        using Process? first = await relay.WaitForConfirmationAsync();
        using Process? second = await relay.WaitForConfirmationAsync();
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null.And.Not.SameAs(first));
        first!.Dispose();
        Assert.DoesNotThrow(() => _ = second!.MainWindowHandle);
        Assert.That(second!.Id, Is.EqualTo(source.Id));
    }

    [Test]
    public async Task ProcessWithoutIdentity_DoesNotPublishInvalidObject()
    {
        GameRuntimeProcessRelay relay = new();
        using Process source = new();
        relay.Track(source);
        Assert.That(relay.OpenCurrentProcess(), Is.Null);
        relay.Confirm(source);
        Assert.That(await relay.WaitForConfirmationAsync(), Is.Null);
    }
}
