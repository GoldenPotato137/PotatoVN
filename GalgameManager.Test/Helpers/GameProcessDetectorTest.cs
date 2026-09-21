using System.Diagnostics;
using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class GameProcessDetectorTest
{
    [Test]
    public void IsProcessIdPresent_FindsCurrentProcessWithoutQueryingTargetState()
    {
        using Process current = Process.GetCurrentProcess();

        Assert.That(GameProcessDetector.IsProcessIdPresent(current.Id), Is.True);
    }

    [Test]
    public void IsProcessIdPresent_RejectsInvalidIds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GameProcessDetector.IsProcessIdPresent(0), Is.False);
            Assert.That(GameProcessDetector.IsProcessIdPresent(-1), Is.False);
        });
    }

    [Test]
    public void WaitForExitSafelyAsync_StillHonorsCancellationForRunningProcess()
    {
        using Process current = Process.GetCurrentProcess();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        Assert.That(async () => await GameProcessDetector.WaitForExitSafelyAsync(current, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
