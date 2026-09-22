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
}
