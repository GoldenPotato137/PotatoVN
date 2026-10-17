using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using Moq;
using Newtonsoft.Json;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class PlayedTimeViewModelTest
{
    [TestCase(false, null)]
    [TestCase(true, null)]
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task LaunchSegmentPreference_DefaultsOffAndPreservesSavedValue(bool precise, bool? savedPreference)
    {
        Mock<ILocalSettingsService> settings = CreateSettings(precise, savedPreference);
        Galgame game = CreateGame();
        string originalRecords = SerializeRecords(game);
        bool expectedPreference = savedPreference ?? false;
        PlayedTimeViewModel viewModel = CreateViewModel(settings, game);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowLaunchSegments, Is.EqualTo(expectedPreference));
            Assert.That(viewModel.Items.Single().Segments, Has.Count.EqualTo(expectedPreference ? 2 : 1));
        });

        await viewModel.ToggleRecordingModeCommand.ExecuteAsync(null);
        PlayedTimeViewModel reloaded = CreateViewModel(settings, game);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.ShowLaunchSegments, Is.EqualTo(expectedPreference));
            Assert.That(reloaded.Items.Single().Segments, Has.Count.EqualTo(expectedPreference ? 2 : 1));
            Assert.That(reloaded.Items.Single().Sessions, Has.Count.EqualTo(precise ? 0 : 1));
            Assert.That(game.PlayTimeSessions.Single(session => session.Kind == PlayTimeSessionKind.Native)
                .ActivityIntervals, Has.Count.EqualTo(2));
            Assert.That(SerializeRecords(game), Is.EqualTo(originalRecords));
        });
        settings.Verify(service => service.SaveSettingAsync(
            KeyValues.PlayedTimeShowLaunchSegments, It.IsAny<bool>(), false, false, null, false), Times.Never);
    }

    [Test]
    public async Task LaunchSegmentPreference_SurvivesModeChangesAndNavigationWithoutChangingRecords()
    {
        Mock<ILocalSettingsService> settings = CreateSettings();
        Galgame game = CreateGame();
        string originalRecords = SerializeRecords(game);
        PlayedTimeViewModel viewModel = CreateViewModel(settings, game);
        Assert.That(viewModel.Items.Single().Segments, Has.Count.EqualTo(2));

        await viewModel.ToggleLaunchSegmentsCommand.ExecuteAsync(null);
        await viewModel.ToggleRecordingModeCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowLaunchSegments, Is.False);
            Assert.That(viewModel.PrecisePlayTimeEnabled, Is.True);
            Assert.That(viewModel.Items.Single().Segments.Single().DurationSeconds, Is.EqualTo(151));
            Assert.That(viewModel.Items.Single().Sessions.Single().ActivityIntervals, Has.Count.EqualTo(2));
        });

        await viewModel.ToggleRecordingModeCommand.ExecuteAsync(null);
        PlayedTimeViewModel reloaded = CreateViewModel(settings, game);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.ShowLaunchSegments, Is.False);
            Assert.That(reloaded.PrecisePlayTimeEnabled, Is.False);
            Assert.That(reloaded.Items.Single().Segments, Has.Count.EqualTo(1));
        });

        await reloaded.ToggleLaunchSegmentsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Items.Single().Segments, Has.Count.EqualTo(2));
            Assert.That(SerializeRecords(game), Is.EqualTo(originalRecords));
        });
    }

    [Test]
    public async Task ToggleLaunchSegments_PreservesExpandedDayAndSessionObjects()
    {
        Mock<ILocalSettingsService> settings = CreateSettings(precise: true);
        PlayedTimeViewModel viewModel = CreateViewModel(settings, CreateGame());
        PlayTimeDayViewModelItem day = viewModel.Items.Single();
        PlayTimeSessionViewModelItem session = day.Sessions.Single();
        string originalTotal = day.TotalText;
        day.IsExpanded = true;
        session.IsExpanded = true;

        await viewModel.ToggleLaunchSegmentsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items.Single(), Is.SameAs(day));
            Assert.That(day.IsExpanded, Is.True);
            Assert.That(day.Sessions.Single(), Is.SameAs(session));
            Assert.That(session.IsExpanded, Is.True);
            Assert.That(session.ActivityIntervals, Has.Count.EqualTo(2));
            Assert.That(day.TotalText, Is.EqualTo(originalTotal));
            Assert.That(day.Segments.Single().ToolTip, Is.EqualTo(originalTotal));
        });

        await viewModel.ToggleLaunchSegmentsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(day.Segments, Has.Count.EqualTo(2));
            Assert.That(day.Sessions.Single(), Is.SameAs(session));
            Assert.That(session.IsExpanded, Is.True);
        });
    }

    [Test]
    public async Task ToggleLaunchSegments_SaveFailurePreservesCurrentPreferenceAndBars()
    {
        Mock<ILocalSettingsService> settings = CreateSettings();
        PlayedTimeViewModel viewModel = CreateViewModel(settings, CreateGame());
        PlayTimeDayViewModelItem day = viewModel.Items.Single();
        PlayTimeBarSegmentViewModelItem[] bars = day.Segments.ToArray();
        settings.Setup(service => service.SaveSettingAsync(
                KeyValues.PlayedTimeShowLaunchSegments, false, false, false, null, false))
            .ThrowsAsync(new IOException("设置保存失败"));

        await viewModel.ToggleLaunchSegmentsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowLaunchSegments, Is.True);
            Assert.That(viewModel.Items.Single(), Is.SameAs(day));
            Assert.That(day.Segments, Is.EqualTo(bars));
        });
    }

    private static Mock<ILocalSettingsService> CreateSettings(bool precise = false, bool? savedPreference = true)
    {
        Dictionary<string, bool> values = new()
        {
            [KeyValues.PrecisePlayTime] = precise,
        };
        if (savedPreference.HasValue) values[KeyValues.PlayedTimeShowLaunchSegments] = savedPreference.Value;
        Mock<ILocalSettingsService> settings = new();
        settings.Setup(service => service.ReadSettingAsync<bool>(
                It.IsAny<string>(), false, null, false))
            .Returns((string key, bool _, List<JsonConverter>? _, bool _) =>
                Task.FromResult(values.TryGetValue(key, out bool value) && value));
        settings.Setup(service => service.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<bool>(), false, false, null, false))
            .Callback((string key, bool value, bool _, bool _, List<JsonConverter>? _, bool _) =>
                values[key] = value)
            .Returns(Task.CompletedTask);
        return settings;
    }

    private static PlayedTimeViewModel CreateViewModel(Mock<ILocalSettingsService> settings, Galgame game)
    {
        PlayedTimeViewModel viewModel = new(
            Mock.Of<INavigationService>(),
            Mock.Of<IGalgameCollectionService>(),
            Mock.Of<IPvnService>(),
            Mock.Of<IInfoService>(),
            settings.Object,
            Mock.Of<IBgTaskService>());
        viewModel.OnNavigatedTo(game);
        return viewModel;
    }

    private static Galgame CreateGame()
    {
        DateTime date = new(2026, 8, 26);
        string key = date.ToString("yyyy/M/d");
        PlayTimeSession precise = new()
        {
            StartedAt = date.AddHours(1),
            EndedAt = date.AddHours(1).AddSeconds(100),
            ActivityIntervals =
            [
                new() { StartedAt = date.AddHours(1), EndedAt = date.AddHours(1).AddSeconds(61) },
                new() { StartedAt = date.AddHours(1).AddSeconds(70), EndedAt = date.AddHours(1).AddSeconds(100) },
            ],
        };
        PlayTimeSession minute = new()
        {
            StartedAt = date.AddHours(2),
            EndedAt = date.AddHours(2).AddMinutes(1),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new() { [key] = 1 },
        };
        Galgame game = new()
        {
            PlayedTime = new() { [key] = 2 },
            PlayedTimeSeconds = new() { [key] = 151 },
            PlayTimeSessions = [precise, minute],
        };
        PlayTimeSessionHelper.RefreshDerivedState(game);
        return game;
    }

    private static string SerializeRecords(Galgame game) => JsonConvert.SerializeObject(new
    {
        game.PlayedTime,
        game.PlayedTimeSeconds,
        game.PlayTimeSessions,
        game.TotalPlayTime,
    });
}
