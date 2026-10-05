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
        Process? confirmed = await relay.WaitForConfirmationAsync();

        Assert.Multiple(() =>
        {
            Assert.That(confirmed, Is.SameAs(process));
            Assert.That(relay.ConfirmedProcess, Is.SameAs(process));
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
        Assert.That(relay.ConfirmedProcess, Is.Null);
    }

    [Test]
    public async Task Track_OnlyChangesTarget_DoesNotStartGameplay()
    {
        GameRuntimeProcessRelay relay = new();
        using Process process = Process.GetCurrentProcess();
        relay.Track(process);
        Task<Process?> confirmation = relay.WaitForConfirmationAsync();
        Assert.Multiple(() =>
        {
            Assert.That(relay.CurrentProcess, Is.SameAs(process));
            Assert.That(relay.ConfirmedProcess, Is.Null);
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
        Assert.That(await confirmation, Is.SameAs(second));
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
            Assert.That(relay.CurrentProcess, Is.Null);
            Assert.That(relay.ConfirmedProcess, Is.Null);
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
            Assert.That(relay.CurrentProcess, Is.Null);
            Assert.That(relay.ConfirmedProcess, Is.Null);
        });
    }
}
