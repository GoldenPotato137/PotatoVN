using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class PlayTimeRecordingModeHelperTest
{
    [TestCase(false, true, true, false)]
    [TestCase(true, false, false, true)]
    [TestCase(null, true, false, true)]
    [TestCase(null, false, true, true)]
    [TestCase(null, false, false, false)]
    public void ResolvePreciseMode_KeepsLockedOrRecoveredLaunchMode(
        bool? lockedMode,
        bool hasActiveSession,
        bool settingEnabled,
        bool expected)
    {
        Assert.That(
            PlayTimeRecordingModeHelper.ResolvePreciseMode(
                lockedMode,
                hasActiveSession,
                settingEnabled),
            Is.EqualTo(expected));
    }
}
