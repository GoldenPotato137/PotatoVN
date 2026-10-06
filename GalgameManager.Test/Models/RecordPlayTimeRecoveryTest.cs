using System.Diagnostics;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
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
