using GalgameManager.Helpers;
using GalgameManager.Core.Helpers;
using GalgameManager.Models;
using GalgameManager.Views.Dialog;
using Newtonsoft.Json;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class PlayTimeSessionHelperTest
{
    [Test]
    public void AddLegacyMinuteSample_SeedsLegacyDataWithoutCreatingSession()
    {
        DateTime lastPlayTime = new(2026, 8, 20, 18, 30, 0);
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/19"] = 2 },
            TotalPlayTime = 2,
            LastPlayTime = lastPlayTime,
        };

        PlayTimeSessionHelper.AddLegacyMinuteSample(game, new DateTime(2026, 8, 19, 20, 0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds["2026/8/19"], Is.EqualTo(180));
            Assert.That(game.PlayedTime["2026/8/19"], Is.EqualTo(3));
            Assert.That(game.TotalPlayTime, Is.EqualTo(3));
            Assert.That(game.LastPlayTime, Is.EqualTo(lastPlayTime));
            Assert.That(game.PlayTimeSessions, Is.Empty);
        });
    }

    [Test]
    public void AddLegacyMinuteSample_PreservesExistingSecondRemainderAndSessions()
    {
        PlayTimeSession session = new()
        {
            StartedAt = new DateTime(2026, 8, 19, 12, 0, 0),
            EndedAt = new DateTime(2026, 8, 19, 12, 0, 31),
        };
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/19"] = 2 },
            PlayedTimeSeconds = new Dictionary<string, long> { ["2026/8/19"] = 151 },
            PlayTimeSessions = [session],
            TotalPlayTime = 2,
        };

        PlayTimeSessionHelper.AddLegacyMinuteSample(game, new DateTime(2026, 8, 19, 20, 0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds["2026/8/19"], Is.EqualTo(211));
            Assert.That(game.PlayedTime["2026/8/19"], Is.EqualTo(3));
            Assert.That(game.TotalPlayTime, Is.EqualTo(3));
            Assert.That(game.PlayTimeSessions, Has.Count.EqualTo(1));
            Assert.That(game.PlayTimeSessions[0].Id, Is.EqualTo(session.Id));
        });
    }

    [Test]
    public void DisplayPlayTime_MinuteModePreservesHiddenSecondRemainder()
    {
        DisplayPlayTime time = new("2026/8/19", 151, 0, false);

        Assert.Multiple(() =>
        {
            Assert.That(time.PlayedTime, Is.EqualTo(2));
            Assert.That(time.SecondsVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Collapsed));
            Assert.That(time.TotalSeconds, Is.EqualTo(151));
        });

        time.PlayedTime = 3;
        Assert.That(time.TotalSeconds, Is.EqualTo(211));
    }

    [Test]
    public void AddInterval_SeedsLegacyMinutesAndKeepsSecondPrecision()
    {
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/19"] = 2 },
            TotalPlayTime = 2,
        };

        PlayTimeSessionHelper.AddInterval(
            game,
            new DateTime(2026, 8, 19, 12, 0, 0),
            new DateTime(2026, 8, 19, 12, 0, 31));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds["2026/8/19"], Is.EqualTo(151));
            Assert.That(game.PlayedTime["2026/8/19"], Is.EqualTo(2));
            Assert.That(game.TotalPlayTime, Is.EqualTo(2));
        });
    }

    [Test]
    public void AddInterval_SplitsAtMidnight()
    {
        Galgame game = new();

        PlayTimeSessionHelper.AddInterval(
            game,
            new DateTime(2026, 8, 19, 23, 59, 45),
            new DateTime(2026, 8, 20, 0, 0, 15));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds["2026/8/19"], Is.EqualTo(15));
            Assert.That(game.PlayedTimeSeconds["2026/8/20"], Is.EqualTo(15));
            Assert.That(game.PlayedTime, Is.Empty);
        });
    }

    [Test]
    public void ExtendSession_UsesWholeSessionBoundaryWithoutSamplingDrift()
    {
        DateTime start = new(2026, 8, 21, 1, 1, 20);
        PlayTimeSession session = new()
        {
            StartedAt = start,
            EndedAt = start,
            IsOpen = true,
            ActivityIntervals = [],
        };
        Galgame game = new() { PlayTimeSessions = [session] };
        PlayTimeActivityInterval interval = PlayTimeSessionHelper.BeginActivityInterval(session, start);
        long countedSeconds = 0;

        for (int i = 1; i <= 90; i++)
            countedSeconds += PlayTimeSessionHelper.ExtendActivityInterval(
                game,
                session,
                interval,
                start.AddMilliseconds(i * 1490));

        long sessionSeconds = PlayTimeSessionHelper.SplitSessionByDay(session)
            .Sum(segment => segment.DurationSeconds);
        Assert.Multiple(() =>
        {
            Assert.That(countedSeconds, Is.EqualTo(134));
            Assert.That(game.PlayedTimeSeconds["2026/8/21"], Is.EqualTo(sessionSeconds));
            Assert.That(sessionSeconds, Is.EqualTo(134));
        });
    }

    [Test]
    public void ExtendSession_KeepsCrossMidnightDailyTotalsAligned()
    {
        DateTime start = new(2026, 8, 21, 23, 59, 58, 600);
        PlayTimeSession session = new()
        {
            StartedAt = start,
            EndedAt = start,
            IsOpen = true,
            ActivityIntervals = [],
        };
        Galgame game = new() { PlayTimeSessions = [session] };
        PlayTimeActivityInterval interval = PlayTimeSessionHelper.BeginActivityInterval(session, start);

        PlayTimeSessionHelper.ExtendActivityInterval(game, session, interval, start.AddSeconds(3.2));

        PlayTimeDaySegment[] segments = PlayTimeSessionHelper.SplitSessionByDay(session).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds["2026/8/21"], Is.EqualTo(1));
            Assert.That(game.PlayedTimeSeconds["2026/8/22"], Is.EqualTo(2));
            Assert.That(game.PlayedTimeSeconds.Values.Sum(), Is.EqualTo(segments.Sum(x => x.DurationSeconds)));
        });
    }

    [Test]
    public void ActivityIntervals_GroupForegroundFragmentsIntoOneLaunchSession()
    {
        DateTime start = new(2026, 8, 22, 10, 0, 0);
        PlayTimeSession session = new()
        {
            StartedAt = start,
            EndedAt = start,
            IsOpen = true,
            ActivityIntervals = [],
        };
        Galgame game = new() { PlayTimeSessions = [session] };

        PlayTimeActivityInterval first = PlayTimeSessionHelper.BeginActivityInterval(session, start);
        PlayTimeSessionHelper.ExtendActivityInterval(game, session, first, start.AddMinutes(2));
        PlayTimeActivityInterval second = PlayTimeSessionHelper.BeginActivityInterval(session, start.AddMinutes(5));
        PlayTimeSessionHelper.ExtendActivityInterval(game, session, second, start.AddMinutes(7));
        session.EndedAt = start.AddMinutes(10);

        PlayTimeDaySegment[] segments = PlayTimeSessionHelper.SplitSessionByDay(session).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(session.ActivityIntervals, Has.Count.EqualTo(2));
            Assert.That(segments, Has.Length.EqualTo(1));
            Assert.That(segments[0].StartedAt, Is.EqualTo(start));
            Assert.That(segments[0].EndedAt, Is.EqualTo(start.AddMinutes(10)));
            Assert.That(segments[0].DurationSeconds, Is.EqualTo(240));
            Assert.That(game.PlayedTimeSeconds["2026/8/22"], Is.EqualTo(240));
            Assert.That(PlayTimeSessionHelper.GetSessionDurationSeconds(session), Is.EqualTo(240));
        });
    }

    [Test]
    public void GetActivityIntervalsForDay_ReturnsOnlyClippedForegroundFragments()
    {
        DateTime firstDay = new(2026, 8, 22, 23, 58, 0);
        PlayTimeSession session = new()
        {
            StartedAt = firstDay,
            EndedAt = firstDay.AddMinutes(12),
            ActivityIntervals =
            [
                new PlayTimeActivityInterval
                {
                    StartedAt = firstDay,
                    EndedAt = firstDay.AddMinutes(4),
                },
                new PlayTimeActivityInterval
                {
                    StartedAt = firstDay.AddMinutes(7),
                    EndedAt = firstDay.AddMinutes(10),
                },
            ],
        };

        PlayTimeActivityInterval[] firstDayIntervals =
            PlayTimeSessionHelper.GetActivityIntervalsForDay(session, firstDay).ToArray();
        PlayTimeActivityInterval[] secondDayIntervals =
            PlayTimeSessionHelper.GetActivityIntervalsForDay(session, firstDay.AddDays(1)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(firstDayIntervals, Has.Length.EqualTo(1));
            Assert.That(firstDayIntervals[0].StartedAt, Is.EqualTo(firstDay));
            Assert.That(firstDayIntervals[0].EndedAt, Is.EqualTo(firstDay.Date.AddDays(1)));
            Assert.That(secondDayIntervals, Has.Length.EqualTo(2));
            Assert.That(secondDayIntervals[0].StartedAt, Is.EqualTo(firstDay.Date.AddDays(1)));
            Assert.That(secondDayIntervals[0].EndedAt, Is.EqualTo(firstDay.AddMinutes(4)));
            Assert.That(secondDayIntervals[1].StartedAt, Is.EqualTo(firstDay.AddMinutes(7)));
        });
    }

    [Test]
    public void AddManualSession_UpdatesExactAndLegacyTotals()
    {
        DateTime start = new(2026, 8, 22, 12, 0, 10);
        PlayTimeSession session = new()
        {
            StartedAt = start,
            EndedAt = start.AddSeconds(75),
            Kind = PlayTimeSessionKind.Manual,
            ActivityIntervals =
            [
                new PlayTimeActivityInterval { StartedAt = start, EndedAt = start.AddSeconds(75) },
            ],
        };
        Galgame game = new();

        PlayTimeSessionHelper.AddSession(game, session);

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayTimeSessions, Has.Count.EqualTo(1));
            Assert.That(game.PlayedTimeSeconds["2026/8/22"], Is.EqualTo(75));
            Assert.That(game.PlayedTime["2026/8/22"], Is.EqualTo(1));
            Assert.That(game.TotalPlayTime, Is.EqualTo(1));
        });
    }

    [Test]
    public void AddSession_RejectsOverlapButAllowsTouchingBoundary()
    {
        DateTime start = new(2026, 8, 23, 10, 0, 0);
        PlayTimeSession existing = new()
        {
            StartedAt = start,
            EndedAt = start.AddHours(1),
        };
        PlayTimeSession overlapping = new()
        {
            StartedAt = start.AddMinutes(30),
            EndedAt = start.AddHours(2),
            Kind = PlayTimeSessionKind.Manual,
        };
        PlayTimeSession touching = new()
        {
            StartedAt = existing.EndedAt,
            EndedAt = existing.EndedAt.AddMinutes(30),
            Kind = PlayTimeSessionKind.Manual,
        };
        Galgame game = new() { PlayTimeSessions = [existing] };

        Assert.Multiple(() =>
        {
            Assert.That(PlayTimeSessionHelper.HasOverlappingSession(game, overlapping), Is.True);
            Assert.That(PlayTimeSessionHelper.HasOverlappingSession(game, touching), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                PlayTimeSessionHelper.AddSession(game, overlapping));
            Assert.That(game.PlayTimeSessions, Has.Count.EqualTo(1));
        });

        PlayTimeSessionHelper.AddSession(game, touching);
        Assert.That(game.PlayTimeSessions, Has.Count.EqualTo(2));
    }

    [Test]
    public void ReplaceSession_RejectsOverlapWithoutChangingExistingTotals()
    {
        DateTime start = new(2026, 8, 23, 10, 0, 0);
        PlayTimeSession first = new()
        {
            StartedAt = start,
            EndedAt = start.AddMinutes(30),
        };
        PlayTimeSession second = new()
        {
            StartedAt = start.AddHours(1),
            EndedAt = start.AddHours(2),
        };
        Galgame game = new() { PlayTimeSessions = [first, second] };
        PlayTimeSessionHelper.AddInterval(game, first.StartedAt, first.EndedAt);
        PlayTimeSessionHelper.AddInterval(game, second.StartedAt, second.EndedAt);
        long originalSeconds = game.PlayedTimeSeconds["2026/8/23"];
        PlayTimeSession replacement = first.Clone();
        replacement.EndedAt = start.AddHours(1.5);

        Assert.Throws<InvalidOperationException>(() =>
            PlayTimeSessionHelper.ReplaceSession(game, first, replacement));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayTimeSessions[0].EndedAt, Is.EqualTo(first.EndedAt));
            Assert.That(game.PlayedTimeSeconds["2026/8/23"], Is.EqualTo(originalSeconds));
        });
    }

    [Test]
    public void ReconcileCountedSessionTotals_RepairsAggregateShorterThanCountedSessions()
    {
        DateTime start = new(2026, 8, 21, 1, 1, 20);
        PlayTimeSession session = new()
        {
            StartedAt = start,
            EndedAt = start.AddSeconds(5480),
        };
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/21"] = 90 },
            PlayedTimeSeconds = new Dictionary<string, long> { ["2026/8/21"] = 5423 },
            PlayTimeSessions = [session],
            TotalPlayTime = 90,
        };

        bool changed = PlayTimeSessionHelper.ReconcileCountedSessionTotals(game);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(game.PlayedTimeSeconds["2026/8/21"], Is.EqualTo(5480));
            Assert.That(game.PlayedTime["2026/8/21"], Is.EqualTo(91));
            Assert.That(game.TotalPlayTime, Is.EqualTo(91));
        });
    }

    [Test]
    public void ReplaceAndDeleteSession_AdjustPreciseAndLegacyTotals()
    {
        DateTime start = new(2026, 8, 19, 20, 0, 0);
        PlayTimeSession original = new()
        {
            StartedAt = start,
            EndedAt = start.AddSeconds(90),
        };
        Galgame game = new() { PlayTimeSessions = [original] };
        PlayTimeSessionHelper.AddInterval(game, original.StartedAt, original.EndedAt);

        PlayTimeSession replacement = original.Clone();
        replacement.EndedAt = start.AddSeconds(150);
        PlayTimeSessionHelper.ReplaceSession(game, original, replacement);

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds["2026/8/19"], Is.EqualTo(150));
            Assert.That(game.PlayedTime["2026/8/19"], Is.EqualTo(2));
        });

        Assert.That(PlayTimeSessionHelper.DeleteSession(game, original.Id), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(game.PlayTimeSessions, Is.Empty);
            Assert.That(game.PlayedTimeSeconds, Is.Empty);
            Assert.That(game.PlayedTime, Is.Empty);
            Assert.That(game.TotalPlayTime, Is.Zero);
        });
    }

    [Test]
    public void ReplaceSession_MissingOriginalDoesNotChangeTotals()
    {
        DateTime start = new(2026, 8, 19, 20, 0, 0);
        PlayTimeSession missing = new()
        {
            StartedAt = start,
            EndedAt = start.AddSeconds(90),
        };
        PlayTimeSession replacement = missing.Clone();
        replacement.EndedAt = start.AddSeconds(150);
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/19"] = 2 },
            PlayedTimeSeconds = new Dictionary<string, long> { ["2026/8/19"] = 120 },
            TotalPlayTime = 2,
        };

        Assert.Throws<InvalidOperationException>(() =>
            PlayTimeSessionHelper.ReplaceSession(game, missing, replacement));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds["2026/8/19"], Is.EqualTo(120));
            Assert.That(game.PlayedTime["2026/8/19"], Is.EqualTo(2));
            Assert.That(game.TotalPlayTime, Is.EqualTo(2));
            Assert.That(game.PlayTimeSessions, Is.Empty);
        });
    }

    [Test]
    public void DeleteSession_RejectsOpenSession()
    {
        PlayTimeSession session = new()
        {
            StartedAt = new DateTime(2026, 8, 19, 20, 0, 0),
            EndedAt = new DateTime(2026, 8, 19, 20, 1, 0),
            IsOpen = true,
        };
        Galgame game = new() { PlayTimeSessions = [session] };

        Assert.That(PlayTimeSessionHelper.DeleteSession(game, session.Id), Is.False);
    }

    [Test]
    public void AddLegacyMinuteSample_TracksMinuteSegmentsWithoutChangingSamplingPrecision()
    {
        DateTime firstDate = new(2026, 8, 23, 23, 59, 30);
        DateTime secondDate = firstDate.AddMinutes(1);
        PlayTimeSession session = new()
        {
            StartedAt = firstDate.AddMinutes(-1),
            EndedAt = firstDate.AddMinutes(-1),
            IsOpen = true,
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
        };
        Galgame game = new() { PlayTimeSessions = [session] };

        PlayTimeSessionHelper.AddLegacyMinuteSample(game, firstDate, session);
        PlayTimeSessionHelper.AddLegacyMinuteSample(game, secondDate, session);

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTime["2026/8/23"], Is.EqualTo(1));
            Assert.That(game.PlayedTime["2026/8/24"], Is.EqualTo(1));
            Assert.That(game.PlayedTimeSeconds["2026/8/23"], Is.EqualTo(60));
            Assert.That(game.PlayedTimeSeconds["2026/8/24"], Is.EqualTo(60));
            Assert.That(PlayTimeSessionHelper.GetMinuteSampleDurationsForDay(game, firstDate),
                Is.EqualTo(new long[] { 60 }));
            Assert.That(PlayTimeSessionHelper.GetMinuteSampleDurationsForDay(game, secondDate),
                Is.EqualTo(new long[] { 60 }));
        });
    }

    [Test]
    public void MinuteSampleSegments_ExposeEachLaunchDurationAndBoundaries()
    {
        DateTime date = new(2026, 8, 23);
        PlayTimeSession first = new()
        {
            StartedAt = date.AddHours(1),
            EndedAt = date.AddHours(1.5),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 3 },
        };
        PlayTimeSession second = new()
        {
            StartedAt = date.AddHours(2),
            EndedAt = date.AddHours(2.5),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 4 },
        };
        Galgame game = new() { PlayTimeSessions = [first, second] };

        MinutePlayTimeDaySegment[] segments =
            PlayTimeSessionHelper.GetMinuteSampleSegmentsForDay(game, date).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(segments, Has.Length.EqualTo(2));
            Assert.That(segments[0].SessionId, Is.EqualTo(first.Id));
            Assert.That(segments[0].StartedAt, Is.EqualTo(first.StartedAt));
            Assert.That(segments[0].EndedAt, Is.EqualTo(first.EndedAt));
            Assert.That(segments[0].Minutes, Is.EqualTo(3));
            Assert.That(segments[0].SpansMultipleDays, Is.False);
            Assert.That(segments[1].SessionId, Is.EqualTo(second.Id));
            Assert.That(segments[1].Minutes, Is.EqualTo(4));
            Assert.That(segments[1].SpansMultipleDays, Is.False);
        });
    }

    [Test]
    public void MinuteSampleSegments_MarkEverySliceOfCrossDayLaunch()
    {
        DateTime firstDate = new(2026, 8, 23);
        DateTime secondDate = firstDate.AddDays(1);
        PlayTimeSession session = new()
        {
            StartedAt = firstDate.AddHours(23).AddMinutes(30),
            EndedAt = secondDate.AddMinutes(30),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int>
            {
                ["2026/8/23"] = 30,
                ["2026/8/24"] = 30,
            },
        };
        Galgame game = new() { PlayTimeSessions = [session] };

        MinutePlayTimeDaySegment firstSegment =
            PlayTimeSessionHelper.GetMinuteSampleSegmentsForDay(game, firstDate).Single();
        MinutePlayTimeDaySegment secondSegment =
            PlayTimeSessionHelper.GetMinuteSampleSegmentsForDay(game, secondDate).Single();

        Assert.Multiple(() =>
        {
            Assert.That(firstSegment.SpansMultipleDays, Is.True);
            Assert.That(secondSegment.SpansMultipleDays, Is.True);
        });
    }

    [Test]
    public void ReplaceMinuteSampleSegment_AdjustsOnlyTargetLaunchAndPreservesRemainder()
    {
        DateTime date = new(2026, 8, 23);
        PlayTimeSession first = new()
        {
            StartedAt = date.AddHours(1),
            EndedAt = date.AddHours(1.5),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 3 },
        };
        PlayTimeSession second = new()
        {
            StartedAt = date.AddHours(2),
            EndedAt = date.AddHours(2.5),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 4 },
        };
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/23"] = 10 },
            PlayedTimeSeconds = new Dictionary<string, long> { ["2026/8/23"] = 645 },
            PlayTimeSessions = [first, second],
        };

        bool changed = PlayTimeSessionHelper.ReplaceMinuteSampleSegment(game, second.Id, date, 6);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(PlayTimeSessionHelper.GetMinuteSampleDurationsForDay(game, date),
                Is.EqualTo(new long[] { 180, 360 }));
            Assert.That(game.PlayedTimeSeconds["2026/8/23"], Is.EqualTo(765));
            Assert.That(game.PlayedTime["2026/8/23"], Is.EqualTo(12));
        });
    }

    [Test]
    public void DayDisplayBreakdown_SeparatesPreciseMinuteAndUnsegmentedTime()
    {
        DateTime date = new(2026, 8, 23);
        PlayTimeSession precise = new()
        {
            StartedAt = date.AddHours(1),
            EndedAt = date.AddHours(1).AddMinutes(10),
        };
        PlayTimeSession firstMinute = new()
        {
            StartedAt = date.AddHours(2),
            EndedAt = date.AddHours(2.5),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 20 },
        };
        PlayTimeSession secondMinute = new()
        {
            StartedAt = date.AddHours(3),
            EndedAt = date.AddHours(3.5),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 10 },
        };
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/23"] = 50 },
            PlayedTimeSeconds = new Dictionary<string, long> { ["2026/8/23"] = 3000 },
            PlayTimeSessions = [precise, firstMinute, secondMinute],
        };

        PlayTimeDayBreakdown breakdown = PlayTimeSessionHelper.GetDayDisplayBreakdown(game, date);

        Assert.Multiple(() =>
        {
            Assert.That(breakdown.TotalSeconds, Is.EqualTo(3000));
            Assert.That(breakdown.PreciseSessionSeconds, Is.EqualTo(600));
            Assert.That(breakdown.MinuteSampleSeconds, Is.EqualTo(1800));
            Assert.That(breakdown.UnsegmentedSeconds, Is.EqualTo(600));
        });
    }

    [Test]
    public void PreciseSessionDaySegments_PreserveSeparateLaunchesForMinuteViewProjection()
    {
        DateTime date = new(2026, 8, 23);
        Galgame game = new()
        {
            PlayTimeSessions =
            [
                new PlayTimeSession
                {
                    StartedAt = date.AddHours(1),
                    EndedAt = date.AddHours(1).AddMinutes(4),
                },
                new PlayTimeSession
                {
                    StartedAt = date.AddHours(2),
                    EndedAt = date.AddHours(2).AddMinutes(7),
                },
                new PlayTimeSession
                {
                    StartedAt = date.AddHours(3),
                    EndedAt = date.AddHours(3).AddMinutes(10),
                    Kind = PlayTimeSessionKind.MinuteSampled,
                    CountsTowardPlayTime = false,
                    SampledMinutesByDay = new Dictionary<string, int>
                    {
                        [date.ToStringDefault()] = 10,
                    },
                },
            ],
        };

        PlayTimeDaySegment[] segments =
            PlayTimeSessionHelper.GetPreciseSessionDaySegments(game).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(segments, Has.Length.EqualTo(2));
            Assert.That(segments.Select(segment => segment.DurationSeconds),
                Is.EqualTo(new long[] { 240, 420 }));
            Assert.That(segments.Select(segment => segment.SessionId).Distinct().Count(),
                Is.EqualTo(2));
        });
    }

    [Test]
    public void ReconcileCountedSessionTotals_PreservesMinuteSamplesAlongsidePreciseSessions()
    {
        DateTime date = new(2026, 8, 23);
        PlayTimeSession precise = new()
        {
            StartedAt = date.AddHours(1),
            EndedAt = date.AddHours(1).AddMinutes(2),
        };
        PlayTimeSession minute = new()
        {
            StartedAt = date.AddHours(2),
            EndedAt = date.AddHours(2.5),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 4 },
        };
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/23"] = 5 },
            PlayedTimeSeconds = new Dictionary<string, long> { ["2026/8/23"] = 300 },
            PlayTimeSessions = [precise, minute],
        };

        bool changed = PlayTimeSessionHelper.ReconcileCountedSessionTotals(game);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(game.PlayedTimeSeconds["2026/8/23"], Is.EqualTo(360));
            Assert.That(game.PlayedTime["2026/8/23"], Is.EqualTo(6));
        });
    }

    [Test]
    public void LargeTotals_SaturateInsteadOfOverflowing()
    {
        DateTime firstDate = new(2026, 8, 23);
        DateTime secondDate = firstDate.AddDays(1);
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int>
            {
                [firstDate.ToStringDefault()] = int.MaxValue,
                [secondDate.ToStringDefault()] = int.MaxValue,
            },
            PlayedTimeSeconds = new Dictionary<string, long>
            {
                [firstDate.ToStringDefault()] = long.MaxValue,
                [secondDate.ToStringDefault()] = long.MaxValue,
            },
        };

        Assert.DoesNotThrow(() => PlayTimeSessionHelper.AddInterval(
            game,
            firstDate.AddHours(1),
            firstDate.AddHours(1).AddSeconds(1)));

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTimeSeconds[firstDate.ToStringDefault()], Is.EqualTo(long.MaxValue));
            Assert.That(game.TotalPlayTime, Is.EqualTo(int.MaxValue));
            Assert.That(PlayTimeSessionHelper.GetTotalSeconds(game), Is.EqualTo(long.MaxValue));
        });
    }

    [Test]
    public void PreciseTotal_IncludesDailySecondRemaindersWithoutChangingCompatibilityMinutes()
    {
        Galgame game = new()
        {
            PlayedTimeSeconds = new Dictionary<string, long>
            {
                ["2026/8/23"] = 119,
                ["2026/8/24"] = 122,
            },
        };

        PlayTimeSessionHelper.RefreshDerivedState(game);

        Assert.Multiple(() =>
        {
            Assert.That(game.PlayedTime.Values.Sum(), Is.EqualTo(3));
            Assert.That(game.TotalPlayTime, Is.EqualTo(3));
            Assert.That(PlayTimeSessionHelper.GetTotalSeconds(game), Is.EqualTo(241));
        });
    }

    [Test]
    public void MergeTime_PreservesLatestPreciseBoundaryAndClampsTotalMinutes()
    {
        DateTime latestBoundary = new(2026, 8, 24, 23, 59, 58);
        Galgame game = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/23"] = int.MaxValue },
        };
        Galgame other = new()
        {
            PlayedTime = new Dictionary<string, int> { ["2026/8/24"] = int.MaxValue },
            LastPlayTime = latestBoundary.AddSeconds(-1),
            PlayTimeSessions =
            [
                new PlayTimeSession
                {
                    StartedAt = latestBoundary.AddMinutes(-1),
                    EndedAt = latestBoundary,
                },
            ],
        };

        Assert.DoesNotThrow(() => game.MergeTime(other));

        Assert.Multiple(() =>
        {
            Assert.That(game.TotalPlayTime, Is.EqualTo(int.MaxValue));
            Assert.That(game.LastPlayTime, Is.EqualTo(latestBoundary));
            Assert.That(game.PlayTimeSessions, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GalgameSerialization_PreservesPreciseSessions()
    {
        PlayTimeSession session = new()
        {
            StartedAt = new DateTime(2026, 8, 19, 20, 0, 0),
            EndedAt = new DateTime(2026, 8, 19, 20, 1, 2),
            Kind = PlayTimeSessionKind.Imported,
            CountsTowardPlayTime = false,
            ActivityIntervals =
            [
                new PlayTimeActivityInterval
                {
                    StartedAt = new DateTime(2026, 8, 19, 20, 0, 5),
                    EndedAt = new DateTime(2026, 8, 19, 20, 0, 55),
                },
            ],
        };
        Galgame game = new()
        {
            PlayedTimeSeconds = new Dictionary<string, long> { ["2026/8/19"] = 62 },
            PlayTimeSessions = [session],
        };

        Galgame restored = JsonConvert.DeserializeObject<Galgame>(JsonConvert.SerializeObject(game))!;

        Assert.Multiple(() =>
        {
            Assert.That(restored.PlayedTimeSeconds["2026/8/19"], Is.EqualTo(62));
            Assert.That(restored.PlayTimeSessions, Has.Count.EqualTo(1));
            Assert.That(restored.PlayTimeSessions[0].Id, Is.EqualTo(session.Id));
            Assert.That(restored.PlayTimeSessions[0].Kind, Is.EqualTo(PlayTimeSessionKind.Imported));
            Assert.That(restored.PlayTimeSessions[0].CountsTowardPlayTime, Is.False);
            Assert.That(restored.PlayTimeSessions[0].ActivityIntervals, Has.Count.EqualTo(1));
            Assert.That(restored.PlayTimeSessions[0].ActivityIntervals![0].StartedAt,
                Is.EqualTo(session.ActivityIntervals![0].StartedAt));
        });
    }

    [Test]
    public void GalgameSerialization_PreservesMinuteSampleSegments()
    {
        PlayTimeSession session = new()
        {
            StartedAt = new DateTime(2026, 8, 23, 20, 0, 0),
            EndedAt = new DateTime(2026, 8, 23, 20, 5, 30),
            Kind = PlayTimeSessionKind.MinuteSampled,
            CountsTowardPlayTime = false,
            SampledMinutesByDay = new Dictionary<string, int> { ["2026/8/23"] = 5 },
            ActivityIntervals = [],
        };
        Galgame game = new() { PlayTimeSessions = [session] };

        Galgame restored = JsonConvert.DeserializeObject<Galgame>(JsonConvert.SerializeObject(game))!;

        Assert.Multiple(() =>
        {
            Assert.That(restored.PlayTimeSessions, Has.Count.EqualTo(1));
            Assert.That(restored.PlayTimeSessions[0].Kind, Is.EqualTo(PlayTimeSessionKind.MinuteSampled));
            Assert.That(restored.PlayTimeSessions[0].SampledMinutesByDay["2026/8/23"], Is.EqualTo(5));
        });
    }
}
