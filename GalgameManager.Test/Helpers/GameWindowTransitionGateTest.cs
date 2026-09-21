using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class GameWindowTransitionGateTest
{
    [Test]
    public void Observe_UnchangedLaunchDialog_RemainsPending()
    {
        GameWindowTransitionGate gate = new();
        GameWindowSnapshot dialog = Snapshot(100, 10, "Dialog", "Disclaimer", 480, 360);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(dialog), Is.False);
            Assert.That(gate.Observe(dialog), Is.False);
            Assert.That(gate.IsReady, Is.False);
            Assert.That(gate.Stage, Is.EqualTo(GameWindowTransitionStage.WaitingForTransition));
        });
    }

    [Test]
    public void Observe_LaunchDialogLayoutChanges_RemainsPending()
    {
        GameWindowTransitionGate gate = new();
        GameWindowSnapshot opening = Snapshot(100, 10, "Dialog", "", 360, 240);
        GameWindowSnapshot ready = Snapshot(100, 10, "Dialog", "Disclaimer", 640, 480);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(opening), Is.False);
            Assert.That(gate.Observe(ready), Is.False);
            Assert.That(gate.Observe(ready), Is.False);
            Assert.That(gate.IsReady, Is.False);
        });
    }

    [Test]
    public void Observe_FirstWindowBelongsToReplacementProcess_RemainsPendingUntilWindowChanges()
    {
        GameWindowTransitionGate gate = new();
        GameWindowSnapshot replacementDialog = Snapshot(200, 20, "#32770", "Disclaimer", 417, 235);
        GameWindowSnapshot game = Snapshot(200, 30, "TVPMainWindow", "Game", 1280, 720);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(replacementDialog), Is.False);
            Assert.That(gate.Observe(replacementDialog), Is.False);
            Assert.That(gate.Observe(replacementDialog), Is.False);
            Assert.That(gate.IsReady, Is.False);
            Assert.That(gate.Observe(game), Is.False);
            Assert.That(gate.Observe(game), Is.True);
        });
    }

    [Test]
    public void Observe_NewWindowMustRemainStableBeforeRecordingStarts()
    {
        GameWindowTransitionGate gate = new();
        GameWindowSnapshot dialog = Snapshot(100, 10, "Dialog", "Disclaimer", 480, 360);
        GameWindowSnapshot game = Snapshot(100, 20, "GameWindow", "Game", 1280, 720);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(dialog), Is.False);
            Assert.That(gate.Observe(dialog), Is.False);
            Assert.That(gate.Observe(game), Is.False);
            Assert.That(gate.Observe(game), Is.True);
            Assert.That(gate.IsReady, Is.True);
        });
    }

    [Test]
    public void Observe_TransientWindowThenDialogAgain_RemainsPending()
    {
        GameWindowTransitionGate gate = new();
        GameWindowSnapshot dialog = Snapshot(100, 10, "Dialog", "Disclaimer", 480, 360);
        GameWindowSnapshot transient = Snapshot(100, 30, "Tooltip", "", 200, 100);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(dialog), Is.False);
            Assert.That(gate.Observe(dialog), Is.False);
            Assert.That(gate.Observe(transient), Is.False);
            Assert.That(gate.Observe(dialog), Is.False);
            Assert.That(gate.IsReady, Is.False);
        });
    }

    [Test]
    public void Observe_LauncherHandsOffToNewProcess_StartsAfterStableSample()
    {
        GameWindowTransitionGate gate = new();
        GameWindowSnapshot launcher = Snapshot(100, 10, "Launcher", "Launcher", 600, 400);
        GameWindowSnapshot game = Snapshot(200, 40, "GameWindow", "Game", 1280, 720);

        Assert.That(gate.Observe(launcher), Is.False);
        Assert.That(gate.Observe(launcher), Is.False);
        Assert.That(gate.Observe(game), Is.False);
        Assert.That(gate.Observe(game), Is.True);
    }

    [Test]
    public void HasMeaningfulTransition_SameWindowTitleOrSizeChangeIsIgnored()
    {
        GameWindowSnapshot baseline = Snapshot(100, 10, "Game", "Launcher", 640, 480);

        Assert.Multiple(() =>
        {
            Assert.That(GameWindowTransitionGate.HasMeaningfulTransition(baseline,
                Snapshot(100, 10, "Game", "Actual game", 640, 480)), Is.False);
            Assert.That(GameWindowTransitionGate.HasMeaningfulTransition(baseline,
                Snapshot(100, 10, "Game", "Launcher", 1280, 720)), Is.False);
            Assert.That(GameWindowTransitionGate.HasMeaningfulTransition(baseline,
                Snapshot(100, 10, "GameWindow", "Launcher", 640, 480)), Is.True);
        });
    }

    [Test]
    public void Observe_WindowDisappearsThenSameIdentityReturns_RemainsPending()
    {
        GameWindowTransitionGate gate = new();
        GameWindowSnapshot window = Snapshot(100, 10, "Game", "Launcher", 640, 480);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(window), Is.False);
            Assert.That(gate.Observe(window), Is.False);
            Assert.That(gate.Observe(null), Is.False);
            Assert.That(gate.Observe(window), Is.False);
            Assert.That(gate.Observe(window), Is.False);
            Assert.That(gate.Stage, Is.EqualTo(GameWindowTransitionStage.WaitingForTransition));
        });
    }

    [Test]
    public void Observe_FastConfirmationHistory_ReachesReadyBeforeConsumerStarts()
    {
        GameLaunchWindowTracker tracker = new();
        GameWindowSnapshot dialog = Snapshot(200, 20, "#32770", "Disclaimer", 417, 235);
        GameWindowSnapshot game = Snapshot(200, 30, "TVPMainWindow", "Game", 1280, 720);

        Assert.Multiple(() =>
        {
            Assert.That(tracker.Observe(dialog), Is.False);
            Assert.That(tracker.Observe(dialog), Is.False);
            Assert.That(tracker.Observe(game), Is.False);
            Assert.That(tracker.Observe(game), Is.True);
            Assert.That(tracker.Stage, Is.EqualTo(GameWindowTransitionStage.Ready));
            Assert.That(tracker.ConfirmedSnapshot, Is.EqualTo(game));
        });

        Assert.That(tracker.DrainLogSnapshots(), Is.EqualTo(new[] { dialog, game }));
    }

    [Test]
    public void Observe_NoPopupSingleWindow_RemainsBaselineWhenWaitingWasExplicitlyEnabled()
    {
        GameLaunchWindowTracker tracker = new();
        GameWindowSnapshot game = Snapshot(200, 30, "GameWindow", "Game", 1280, 720);

        for (int i = 0; i < 10; i++) Assert.That(tracker.Observe(game), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(tracker.Stage, Is.EqualTo(GameWindowTransitionStage.WaitingForTransition));
            Assert.That(tracker.ConfirmedSnapshot, Is.Null);
        });
    }

    [Test]
    public void Observe_ReplacementDialogWithoutTransition_RemainsPending()
    {
        GameLaunchWindowTracker tracker = new();
        GameWindowSnapshot replacementDialog = Snapshot(200, 20, "#32770", "Disclaimer", 417, 235);

        for (int i = 0; i < 10; i++) Assert.That(tracker.Observe(replacementDialog), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(tracker.Stage, Is.EqualTo(GameWindowTransitionStage.WaitingForTransition));
            Assert.That(tracker.ConfirmedSnapshot, Is.Null);
        });
    }

    [Test]
    public void Observe_RejectedDialogDoesNotConfirmGameplayWindow()
    {
        GameLaunchWindowTracker tracker = new();
        GameWindowSnapshot dialog = Snapshot(200, 20, "#32770", "Disclaimer", 417, 235);

        Assert.That(tracker.Observe(dialog), Is.False);
        Assert.That(tracker.Observe(dialog), Is.False);
        for (int i = 0; i < 10; i++) Assert.That(tracker.Observe(null), Is.False);

        Assert.That(tracker.ConfirmedSnapshot, Is.Null);
    }

    [Test]
    public void Observe_ReplacementProcessGameWindow_UsesCompleteLaunchHistory()
    {
        GameLaunchWindowTracker tracker = new();
        GameWindowSnapshot dialog = Snapshot(200, 20, "#32770", "Disclaimer", 417, 235);
        GameWindowSnapshot game = Snapshot(300, 30, "GameWindow", "Game", 1280, 720);

        Assert.That(tracker.Observe(dialog), Is.False);
        Assert.That(tracker.Observe(dialog), Is.False);
        Assert.That(tracker.Observe(game), Is.False);
        Assert.That(tracker.Observe(game), Is.True);

        Assert.That(tracker.ConfirmedSnapshot?.ProcessId, Is.EqualTo(300));
    }

    [Test]
    public void Observe_LauncherThenStandardDialog_WaitsForFollowingGameWindow()
    {
        GameLaunchWindowTracker tracker = new();
        GameWindowSnapshot launcher = Snapshot(100, 10, "LauncherWindow", "Launcher", 640, 480);
        GameWindowSnapshot dialog = Snapshot(200, 20, "#32770", "Disclaimer", 417, 235);
        GameWindowSnapshot game = Snapshot(200, 30, "GameWindow", "Game", 1280, 720);

        Assert.That(tracker.Observe(launcher), Is.False);
        Assert.That(tracker.Observe(launcher), Is.False);
        Assert.That(tracker.Observe(dialog), Is.False);
        Assert.That(tracker.Observe(dialog), Is.False);
        Assert.That(tracker.ConfirmedSnapshot, Is.Null);
        Assert.That(tracker.Observe(game), Is.False);
        Assert.That(tracker.Observe(game), Is.True);

        Assert.That(tracker.ConfirmedSnapshot, Is.EqualTo(game));
    }

    [Test]
    public void StableProcessHandoffGate_RequiresTwoSamplesFromSameDifferentProcess()
    {
        StableProcessHandoffGate gate = new();

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(100, 100), Is.False);
            Assert.That(gate.Observe(100, 200), Is.False);
            Assert.That(gate.Observe(100, 300), Is.False);
            Assert.That(gate.Observe(100, 300), Is.True);
        });
    }

    [Test]
    public void StableProcessHandoffGate_MissingForegroundProcessResetsCandidate()
    {
        StableProcessHandoffGate gate = new();

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(100, 200), Is.False);
            Assert.That(gate.Observe(100, null), Is.False);
            Assert.That(gate.Observe(100, 200), Is.False);
            Assert.That(gate.Observe(100, 200), Is.True);
        });
    }

    private static GameWindowSnapshot Snapshot(int processId, nint windowHandle, string className, string title,
        int width, int height) => new(processId, windowHandle, className, title, width, height);
}

[TestFixture]
public class StableGameWindowGateTest
{
    [Test]
    public void Observe_RequiresTwoStableUsableSamples()
    {
        StableGameWindowGate gate = new();
        GameWindowSnapshot window = new(42, (nint)123, "GameWindow", "Game", 1280, 720);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(window), Is.False);
            Assert.That(gate.Observe(window), Is.True);
        });
    }

    [Test]
    public void Observe_ResetsWhenWindowChanges()
    {
        StableGameWindowGate gate = new();
        GameWindowSnapshot first = new(42, (nint)123, "GameWindow", "Game", 1280, 720);
        GameWindowSnapshot second = first with { WindowHandle = (nint)456 };

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(first), Is.False);
            Assert.That(gate.Observe(second), Is.False);
            Assert.That(gate.Observe(second), Is.True);
        });
    }
}

[TestFixture]
public class GameSessionExitPolicyTest
{
    [TestCase(false, 0, 42, true)]
    [TestCase(true, 0, 42, true)]
    [TestCase(true, 99, 42, true)]
    [TestCase(true, 42, 42, false)]
    public void ShouldWaitForReplacement_OnlySkipsConfirmedGameplayPid(
        bool recordingStarted,
        int confirmedPid,
        int exitedPid,
        bool expected)
    {
        Assert.That(GameSessionExitPolicy.ShouldWaitForReplacement(recordingStarted, confirmedPid, exitedPid),
            Is.EqualTo(expected));
    }
}
