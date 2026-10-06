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

}
