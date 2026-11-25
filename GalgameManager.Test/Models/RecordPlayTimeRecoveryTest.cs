using System.Diagnostics;
using System.Reflection;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using Moq;
using Newtonsoft.Json;

namespace GalgameManager.Test.Models;

[TestFixture]
public class RecordPlayTimeRecoveryTest : ServiceTestBase
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Recover_ZeroMinutes_KeepsPersistedRecordingStage(bool started)
    {
        using Process current = Process.GetCurrentProcess();
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object)
        {
            Galgame = new Galgame(), ProcessId = current.Id, ProcessStartTime = current.StartTime,
            DelayPlayTimeUntilMainWindow = true, RecordingStarted = started,
        };
        string json = JsonConvert.SerializeObject(new
        {
            task.ProcessId, task.ProcessStartTime, task.DelayPlayTimeUntilMainWindow, task.RecordingStarted,
            LaunchWindowBaseline = new GalgameManager.Helpers.GameWindowSnapshot(current.Id, 123, "#32770", "", 400, 300),
        });
        RecordPlayTimeTask restored = new(Settings, GalgameCollectionService.Object) { Galgame = task.Galgame };
        JsonConvert.PopulateObject(json, restored);
        await restored.RecoverFromJson();
        Assert.Multiple(() =>
        {
            Assert.That(restored.RecordingStarted, Is.EqualTo(started));
            Assert.That(restored.ProcessId, Is.EqualTo(current.Id));
            Assert.That(restored.LaunchWindowBaseline?.WindowHandle, Is.EqualTo(123));
            Assert.That(restored.CurrentPlayTime, Is.Zero);
        });
    }

    [TestCase(false, 0, true)]
    [TestCase(true, 0, false)]
    [TestCase(true, 1, true)]
    public async Task Recover_LegacyTask_UsesConservativeStageFallback(bool delay, int minutes, bool expected)
    {
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object)
        {
            DelayPlayTimeUntilMainWindow = delay, CurrentPlayTime = minutes,
        };
        Assert.That(task.RecordingStarted, Is.Null);
        await task.RecoverFromJson();
        Assert.That(task.RecordingStarted, Is.EqualTo(expected));
    }

    [TestCase(1, false, false)]
    [TestCase(0, true, false)]
    [TestCase(0, false, true)]
    public async Task Recover_ZeroMinutes_WithRecordedSession_ResumesRecordingStage(
        long seconds, bool nativeSession, bool minuteSession)
    {
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object)
        {
            DelayPlayTimeUntilMainWindow = true,
            CurrentPlayTimeSeconds = seconds,
            ActiveSessionId = nativeSession ? Guid.NewGuid() : null,
            ActiveMinuteSessionId = minuteSession ? Guid.NewGuid() : null,
        };

        await task.RecoverFromJson();

        Assert.Multiple(() =>
        {
            Assert.That(task.CurrentPlayTime, Is.Zero);
            Assert.That(task.RecordingStarted, Is.True);
        });
    }

    [TestCase("BeginSessionAsync", TestName = "BeginNativeSession_SaveFails_RemovesOnlyUnpersistedSession")]
    [TestCase("BeginMinuteSessionAsync", TestName = "BeginMinuteSession_SaveFails_RemovesOnlyUnpersistedSession")]
    public void BeginSession_SaveFails_RemovesOnlyUnpersistedSession(string methodName)
    {
        DateTime startedAt = new(2026, 1, 1, 12, 0, 0);
        PlayTimeSession previous = new()
        {
            StartedAt = startedAt.AddDays(-1), EndedAt = startedAt.AddDays(-1).AddMinutes(1),
        };
        Galgame game = new() { PlayTimeSessions = [previous] };
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object) { Galgame = game };
        GalgameCollectionService.Setup(service => service.SaveGalgameAsync(game))
            .ThrowsAsync(new IOException("模拟持久化失败"));

        Assert.ThrowsAsync<IOException>(() => InvokePrivateAsync(task, methodName, startedAt));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayTimeSessions, Is.EqualTo(new[] { previous }));
            Assert.That(task.ActiveSessionId, Is.Null);
            Assert.That(task.ActiveMinuteSessionId, Is.Null);
        });
        GalgameCollectionService.Verify(service => service.SaveGalgameAsync(game), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Recover_MissingProcess_ClosesSessionAtPersistedBoundary(bool minuteMode)
    {
        DateTime startedAt = new(2026, 1, 1, 12, 0, 0);
        DateTime persistedEnd = startedAt.AddSeconds(30);
        PlayTimeSession session = new()
        {
            StartedAt = startedAt, EndedAt = persistedEnd, IsOpen = true,
            Kind = minuteMode ? PlayTimeSessionKind.MinuteSampled : PlayTimeSessionKind.Native,
            CountsTowardPlayTime = !minuteMode,
            ActivityIntervals = [new() { StartedAt = startedAt, EndedAt = persistedEnd }],
        };
        Galgame game = new() { PlayTimeSessions = [session] };
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object)
        {
            Galgame = game, ProcessId = 0, RecordingStarted = true,
            ActiveSessionId = minuteMode ? null : session.Id,
            ActiveMinuteSessionId = minuteMode ? session.Id : null,
        };

        await task.RecoverFromJson();
        await InvokePrivateAsync(task, "RunCoreAsync");

        Assert.Multiple(() =>
        {
            Assert.That(session.IsOpen, Is.False);
            Assert.That(session.EndedAt, Is.EqualTo(persistedEnd));
            Assert.That(task.ActiveSessionId, Is.Null);
            Assert.That(task.ActiveMinuteSessionId, Is.Null);
            Assert.That(game.PlayTimeSessions, minuteMode ? Is.Empty : Is.EqualTo(new[] { session }));
            Assert.That(task.CurrentPlayTimeSeconds, Is.Zero);
        });
        GalgameCollectionService.Verify(service => service.SaveGalgameAsync(game), Times.Once);
    }

    private static Task InvokePrivateAsync(RecordPlayTimeTask task, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(RecordPlayTimeTask).GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(task, arguments)!;
    }

    [Test]
    public async Task Recover_ReusedPid_DoesNotAttachAnotherGameRun()
    {
        using Process current = Process.GetCurrentProcess();
        DateTime previousStart = current.StartTime.AddDays(-1);
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object)
        {
            ProcessId = current.Id, ProcessStartTime = previousStart, RecordingStarted = true,
        };
        await task.RecoverFromJson();
        Assert.That(task.ProcessStartTime, Is.EqualTo(previousStart));
        Assert.That(typeof(RecordPlayTimeTask).GetField("_process",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(task), Is.Null);
    }
}
