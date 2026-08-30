using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.ViewModels;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class PlayedTimeViewModelItemTest
{
    [Test]
    public void MinuteModeSubMinuteTotal_RemainsVisibleWithoutRoundingUp()
    {
        PlayTimeDayViewModelItem item = new(
            new DateTime(2026, 8, 26),
            31,
            0,
            [],
            [],
            false,
            false,
            true);

        Assert.Multiple(() =>
        {
            Assert.That(item.TotalSeconds, Is.EqualTo(31));
            Assert.That(item.TotalText, Is.EqualTo("<1m"));
        });
    }

    [Test]
    public void ApplySnapshot_PreservesExpandedObjectsAndUpdatesDisplayedValues()
    {
        DateTime date = new(2026, 8, 26);
        PlayTimeSession session = new()
        {
            StartedAt = date.AddHours(1),
            EndedAt = date.AddHours(1).AddMinutes(1),
            ActivityIntervals =
            [
                new PlayTimeActivityInterval
                {
                    StartedAt = date.AddHours(1),
                    EndedAt = date.AddHours(1).AddMinutes(1),
                },
            ],
        };
        PlayTimeSessionViewModelItem existingSession = CreateSessionItem(session, date, 60, true);
        PlayTimeDayViewModelItem existingDay = CreateDayItem(date, 60, existingSession, true);

        session.EndedAt = date.AddHours(1).AddMinutes(2);
        session.ActivityIntervals![0].EndedAt = session.EndedAt;
        PlayTimeSessionViewModelItem updatedSession = CreateSessionItem(session, date, 120, false);
        PlayTimeDayViewModelItem updatedDay = CreateDayItem(date, 120, updatedSession, false);

        existingDay.ApplySnapshot(updatedDay);

        Assert.Multiple(() =>
        {
            Assert.That(existingDay.IsExpanded, Is.True);
            Assert.That(existingDay.TotalSeconds, Is.EqualTo(120));
            Assert.That(existingDay.TotalText, Is.EqualTo(updatedDay.TotalText));
            Assert.That(existingDay.Sessions, Has.Count.EqualTo(1));
            Assert.That(existingDay.Sessions[0], Is.SameAs(existingSession));
            Assert.That(existingSession.IsExpanded, Is.True);
            Assert.That(existingSession.Duration, Is.EqualTo(updatedSession.Duration));
            Assert.That(existingSession.ActivityIntervals, Has.Count.EqualTo(1));
        });
    }

    private static PlayTimeDayViewModelItem CreateDayItem(
        DateTime date,
        long seconds,
        PlayTimeSessionViewModelItem session,
        bool isExpanded) => new(
        date,
        seconds,
        0,
        [],
        [session],
        true,
        true,
        false,
        isExpanded);

    private static PlayTimeSessionViewModelItem CreateSessionItem(
        PlayTimeSession session,
        DateTime date,
        long seconds,
        bool isExpanded) => new(
        session,
        new PlayTimeDaySegment(
            session.Id,
            date,
            session.StartedAt,
            session.EndedAt,
            seconds,
            session.IsOpen,
            session.Kind,
            session.CountsTowardPlayTime),
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        false,
        isExpanded);
}
