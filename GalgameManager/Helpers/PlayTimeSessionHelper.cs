using GalgameManager.Core.Helpers;
using GalgameManager.Models;

namespace GalgameManager.Helpers;

/// <summary>
/// 保持原生精确时段与旧版逐日分钟兼容层一致。
/// </summary>
public static class PlayTimeSessionHelper
{
    public static long GetDaySeconds(Galgame game, DateTime date)
    {
        string key = date.Date.ToStringDefault();
        if (game.PlayedTimeSeconds.TryGetValue(key, out long seconds)) return Math.Max(0, seconds);
        return Math.Max(0, GetLegacyMinutes(game, date) * 60L);
    }

    public static int GetDayMinutes(Galgame game, DateTime date) => GetLegacyMinutes(game, date);

    /// <summary>
    /// 按旧版规则为指定日期增加一次整分钟采样，同时维护秒级兼容汇总。
    /// 传入分钟级启动记录时，同时保存本次启动对柱状图分段的贡献；
    /// 逐日总时长仍只增加一个整分钟，不会因此获得秒级精度。
    /// </summary>
    public static void AddLegacyMinuteSample(
        Galgame game,
        DateTime date,
        PlayTimeSession? minuteSession = null)
    {
        string key = date.Date.ToStringDefault();
        EnsureSecondBucket(game, date.Date, key);
        long currentSeconds = Math.Max(0, game.PlayedTimeSeconds[key]);
        long updatedSeconds = currentSeconds > long.MaxValue - 60 ? long.MaxValue : currentSeconds + 60;
        game.PlayedTimeSeconds[key] = updatedSeconds;
        game.PlayedTime[key] = checked((int)Math.Min(int.MaxValue, updatedSeconds / 60));
        long totalMinutes = game.PlayedTime.Values.Sum(value => (long)Math.Max(0, value));
        game.TotalPlayTime = checked((int)Math.Min(int.MaxValue, totalMinutes));

        if (minuteSession is null) return;
        minuteSession.SampledMinutesByDay ??= new Dictionary<string, int>();
        int currentMinutes = minuteSession.SampledMinutesByDay.TryGetValue(key, out int value)
            ? Math.Max(0, value)
            : 0;
        minuteSession.SampledMinutesByDay[key] = currentMinutes == int.MaxValue
            ? int.MaxValue
            : currentMinutes + 1;
        if (minuteSession.EndedAt < date) minuteSession.EndedAt = date;
    }

    /// <summary>
    /// 返回指定日期内由分钟级启动记录贡献的各段信息。
    /// 旧数据和手工补差不属于任何启动，因此不会出现在结果中。
    /// </summary>
    public static IReadOnlyList<MinutePlayTimeDaySegment> GetMinuteSampleSegmentsForDay(
        Galgame game,
        DateTime date)
    {
        DateTime dayStart = date.Date;
        DateTime dayEnd = dayStart.AddDays(1);
        return game.PlayTimeSessions
            .Where(session => session.Kind == PlayTimeSessionKind.MinuteSampled)
            .OrderBy(session => session.StartedAt)
            .Select(session => new MinutePlayTimeDaySegment(
                session.Id,
                dayStart,
                session.StartedAt > dayStart ? session.StartedAt : dayStart,
                session.EndedAt < dayEnd ? session.EndedAt : dayEnd,
                GetMinuteSamplesForDay(session, dayStart),
                session.IsOpen,
                session.SampledMinutesByDay?.Count(pair => pair.Value > 0) > 1))
            .Where(segment => segment.Minutes > 0)
            .ToArray();
    }

    public static IReadOnlyList<long> GetMinuteSampleDurationsForDay(Galgame game, DateTime date) =>
        GetMinuteSampleSegmentsForDay(game, date)
            .Select(segment => segment.Minutes * 60L)
            .ToArray();

    /// <summary>
    /// 返回所有秒级启动记录按日期拆分后的片段。
    /// 分钟界面与秒级界面共用这份投影，避免秒级启动记录在分钟界面被并入历史余量。
    /// </summary>
    public static IReadOnlyList<PlayTimeDaySegment> GetPreciseSessionDaySegments(Galgame game) =>
        game.PlayTimeSessions
            .Where(session => session.Kind != PlayTimeSessionKind.MinuteSampled)
            .SelectMany(SplitSessionByDay)
            .OrderBy(segment => segment.Date)
            .ThenBy(segment => segment.StartedAt)
            .ToArray();

    public static long GetMinuteSampleSecondsForDay(Galgame game, DateTime date)
    {
        long result = 0;
        foreach (MinutePlayTimeDaySegment segment in GetMinuteSampleSegmentsForDay(game, date))
        {
            long seconds = segment.Minutes * 60L;
            if (result > long.MaxValue - seconds) return long.MaxValue;
            result += seconds;
        }
        return result;
    }

    /// <summary>
    /// 将指定日期的汇总拆分为精确启动记录、分钟模式启动记录和无边界余量。
    /// 分钟模式启动记录只提供整分钟贡献，不会被误当成历史或手工补差。
    /// </summary>
    public static PlayTimeDayBreakdown GetDayDisplayBreakdown(Galgame game, DateTime date)
    {
        DateTime day = date.Date;
        long totalSeconds = GetDaySeconds(game, day);
        long preciseSessionSeconds = 0;
        foreach (PlayTimeSession session in game.PlayTimeSessions.Where(session =>
                     session.Kind != PlayTimeSessionKind.MinuteSampled && session.CountsTowardPlayTime))
        {
            foreach (PlayTimeDaySegment segment in SplitSessionByDay(session).Where(segment => segment.Date == day))
                preciseSessionSeconds = SaturatingAdd(preciseSessionSeconds, segment.DurationSeconds);
        }

        long availableAfterPreciseSessions = Math.Max(0, totalSeconds - preciseSessionSeconds);
        long minuteSampleSeconds = Math.Min(availableAfterPreciseSessions,
            GetMinuteSampleSecondsForDay(game, day));
        long unsegmentedSeconds = Math.Max(0, availableAfterPreciseSessions - minuteSampleSeconds);
        return new PlayTimeDayBreakdown(
            totalSeconds,
            preciseSessionSeconds,
            minuteSampleSeconds,
            unsegmentedSeconds);
    }

    /// <summary>
    /// 修改某次启动在指定日期贡献的整分钟数，并保留当天未归入启动记录的汇总余量。
    /// </summary>
    public static bool ReplaceMinuteSampleSegment(
        Galgame game,
        Guid sessionId,
        DateTime date,
        int minutes)
    {
        PlayTimeSession? session = game.PlayTimeSessions.FirstOrDefault(item =>
            item.Id == sessionId && item.Kind == PlayTimeSessionKind.MinuteSampled);
        if (session is null || session.IsOpen) return false;

        DateTime day = date.Date;
        int normalizedMinutes = Math.Max(0, minutes);
        int previousMinutes = GetMinuteSamplesForDay(session, day);
        long sampledSeconds = GetMinuteSampleSecondsForDay(game, day);
        long unsegmentedSeconds = Math.Max(0, GetDaySeconds(game, day) - sampledSeconds);

        SetMinuteSamplesForDay(session, day, normalizedMinutes);
        if (!HasMinuteSamples(session)) game.PlayTimeSessions.Remove(session);

        long remainingSampledSeconds = Math.Max(0, sampledSeconds - previousMinutes * 60L);
        long replacementSeconds = normalizedMinutes * 60L;
        long newTotal = SaturatingAdd(unsegmentedSeconds,
            SaturatingAdd(remainingSampledSeconds, replacementSeconds));
        SetDaySeconds(game, day, newTotal);
        RefreshDerivedState(game);
        return true;
    }

    public static bool HasMinuteSamples(PlayTimeSession session) =>
        session.SampledMinutesByDay?.Values.Any(value => value > 0) == true;

    public static long GetTotalSeconds(Galgame game)
    {
        HashSet<DateTime> dates = game.PlayedTime.Keys
            .Select(Utils.TryParseDateGuessCulture)
            .Where(date => date.Year > 1900)
            .Select(date => date.Date)
            .ToHashSet();
        foreach (string key in game.PlayedTimeSeconds.Keys)
        {
            DateTime date = Utils.TryParseDateGuessCulture(key);
            if (date.Year > 1900) dates.Add(date.Date);
        }
        return dates.Aggregate(0L, (total, date) =>
            SaturatingAdd(total, GetDaySeconds(game, date)));
    }

    public static void AddInterval(Galgame game, DateTime start, DateTime end)
        => ApplyIntervalDelta(game, start, end, 1);

    public static void RemoveInterval(Galgame game, DateTime start, DateTime end)
        => ApplyIntervalDelta(game, start, end, -1);

    public static void AddSession(Galgame game, PlayTimeSession session)
    {
        if (game.PlayTimeSessions.Any(item => item.Id == session.Id))
            throw new InvalidOperationException("The play-time session already exists.");
        if (HasOverlappingSession(game, session))
            throw new InvalidOperationException("The play-time session overlaps an existing session.");
        if (session.CountsTowardPlayTime) ApplySessionDeltaCore(game, session, 1);
        game.PlayTimeSessions.Add(session);
        RefreshDerivedState(game);
    }

    public static void ReplaceSession(Galgame game, PlayTimeSession original, PlayTimeSession replacement)
    {
        int index = game.PlayTimeSessions.FindIndex(session => session.Id == original.Id);
        if (index < 0) throw new InvalidOperationException("The play-time session no longer exists.");
        if (HasOverlappingSession(game, replacement))
            throw new InvalidOperationException("The play-time session overlaps an existing session.");

        if (original.CountsTowardPlayTime) ApplySessionDeltaCore(game, original, -1);
        if (replacement.CountsTowardPlayTime) ApplySessionDeltaCore(game, replacement, 1);

        replacement.Id = original.Id;
        game.PlayTimeSessions[index] = replacement;
        RefreshDerivedState(game);
    }

    public static bool DeleteSession(Galgame game, Guid sessionId)
    {
        int index = game.PlayTimeSessions.FindIndex(session => session.Id == sessionId);
        if (index < 0) return false;
        PlayTimeSession session = game.PlayTimeSessions[index];
        if (session.IsOpen) return false;
        if (session.CountsTowardPlayTime) ApplySessionDeltaCore(game, session, -1);
        game.PlayTimeSessions.RemoveAt(index);
        RefreshDerivedState(game);
        return true;
    }

    /// <summary>
    /// 将旧格式时段转换为显式计时片段。转换只改变表示方式，不改变累计时长。
    /// </summary>
    public static void EnsureExplicitActivityIntervals(PlayTimeSession session)
    {
        if (session.ActivityIntervals is not null) return;
        session.ActivityIntervals = [];
        if (session.EndedAt > session.StartedAt)
        {
            session.ActivityIntervals.Add(new PlayTimeActivityInterval
            {
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
            });
        }
    }

    public static PlayTimeActivityInterval BeginActivityInterval(PlayTimeSession session, DateTime startedAt)
    {
        EnsureExplicitActivityIntervals(session);
        PlayTimeActivityInterval interval = new()
        {
            StartedAt = startedAt,
            EndedAt = startedAt,
        };
        session.ActivityIntervals!.Add(interval);
        if (session.EndedAt < startedAt) session.EndedAt = startedAt;
        return interval;
    }

    /// <summary>
    /// 延长单次启动中的当前有效计时片段，并返回本次增加到逐日汇总中的秒数。
    /// 外层时段始终保持一条，前后台切换只会新增内部片段。
    /// </summary>
    public static long ExtendActivityInterval(Galgame game, PlayTimeSession session,
        PlayTimeActivityInterval interval, DateTime endedAt)
    {
        if (endedAt <= interval.EndedAt || endedAt <= interval.StartedAt) return 0;

        long previousSeconds = session.CountsTowardPlayTime
            ? GetIntervalSeconds(interval.StartedAt, interval.EndedAt)
            : 0;
        if (session.CountsTowardPlayTime)
            ApplyIntervalDeltaCore(game, interval.StartedAt, interval.EndedAt, -1);

        interval.EndedAt = endedAt;
        if (session.EndedAt < endedAt) session.EndedAt = endedAt;

        long currentSeconds = session.CountsTowardPlayTime
            ? GetIntervalSeconds(interval.StartedAt, interval.EndedAt)
            : 0;
        if (session.CountsTowardPlayTime)
            ApplyIntervalDeltaCore(game, interval.StartedAt, interval.EndedAt, 1);
        RefreshDerivedState(game);
        return Math.Max(0, currentSeconds - previousSeconds);
    }

    public static long GetSessionDurationSeconds(PlayTimeSession session) =>
        GetEffectiveActivityIntervals(session)
            .Aggregate(0L, (total, interval) =>
                SaturatingAdd(total, GetIntervalSeconds(interval.StartedAt, interval.EndedAt)));

    public static bool HasOverlappingSession(Galgame game, PlayTimeSession candidate)
    {
        if (candidate.EndedAt <= candidate.StartedAt) return false;
        return game.PlayTimeSessions.Any(existing =>
            existing.Id != candidate.Id &&
            existing.EndedAt > existing.StartedAt &&
            candidate.StartedAt < existing.EndedAt &&
            candidate.EndedAt > existing.StartedAt);
    }

    /// <summary>
    /// 获取单次启动在指定日期内的有效计时片段，并将跨日片段裁剪到当天边界。
    /// </summary>
    public static IReadOnlyList<PlayTimeActivityInterval> GetActivityIntervalsForDay(
        PlayTimeSession session, DateTime date)
    {
        DateTime dayStart = date.Date;
        DateTime dayEnd = dayStart.AddDays(1);
        return GetEffectiveActivityIntervals(session)
            .Select(interval => new PlayTimeActivityInterval
            {
                StartedAt = interval.StartedAt > dayStart ? interval.StartedAt : dayStart,
                EndedAt = interval.EndedAt < dayEnd ? interval.EndedAt : dayEnd,
            })
            .Where(interval => interval.EndedAt > interval.StartedAt)
            .OrderBy(interval => interval.StartedAt)
            .ToArray();
    }

    public static bool CloseOpenSession(Galgame game, Guid sessionId)
    {
        PlayTimeSession? session = game.PlayTimeSessions.FirstOrDefault(item => item.Id == sessionId);
        if (session is null || !session.IsOpen) return false;
        session.IsOpen = false;
        if (session.EndedAt < session.StartedAt) session.EndedAt = session.StartedAt;
        RefreshDerivedState(game);
        return true;
    }

    public static IReadOnlyList<PlayTimeDaySegment> SplitSessionByDay(PlayTimeSession session)
    {
        List<ActivityDaySlice> slices = GetEffectiveActivityIntervals(session)
            .SelectMany(SplitActivityByDay)
            .ToList();
        if (slices.Count == 0)
        {
            return
            [
                new PlayTimeDaySegment(
                    session.Id,
                    session.StartedAt.Date,
                    session.StartedAt,
                    session.EndedAt < session.StartedAt ? session.StartedAt : session.EndedAt,
                    0,
                    session.IsOpen,
                    session.Kind,
                    session.CountsTowardPlayTime),
            ];
        }

        List<PlayTimeDaySegment> result = [];
        foreach (IGrouping<DateTime, ActivityDaySlice> group in slices.GroupBy(slice => slice.Date))
        {
            DateTime date = group.Key;
            DateTime displayedStart = session.StartedAt > date ? session.StartedAt : date;
            DateTime displayedEnd = session.EndedAt < date.AddDays(1) ? session.EndedAt : date.AddDays(1);
            if (displayedEnd < displayedStart)
            {
                displayedStart = group.Min(slice => slice.StartedAt);
                displayedEnd = group.Max(slice => slice.EndedAt);
            }
            result.Add(new PlayTimeDaySegment(
                session.Id,
                date,
                displayedStart,
                displayedEnd,
                group.Aggregate(0L, (total, slice) => SaturatingAdd(total, slice.DurationSeconds)),
                session.IsOpen,
                session.Kind,
                session.CountsTowardPlayTime));
        }
        return result;
    }

    public static bool RefreshDerivedState(Galgame game)
    {
        bool changed = false;
        foreach ((string key, long value) in game.PlayedTimeSeconds.ToArray())
        {
            long seconds = Math.Max(0, value);
            if (seconds == 0)
            {
                changed = true;
                game.PlayedTimeSeconds.Remove(key);
                game.PlayedTime.Remove(key);
                continue;
            }

            int minutes = checked((int)Math.Min(int.MaxValue, seconds / 60));
            if (minutes > 0)
            {
                if (!game.PlayedTime.TryGetValue(key, out int currentMinutes) || currentMinutes != minutes)
                {
                    game.PlayedTime[key] = minutes;
                    changed = true;
                }
            }
            else if (game.PlayedTime.Remove(key))
            {
                changed = true;
            }
        }

        long totalMinutes = game.PlayedTime.Values.Aggregate(0L, (total, minutes) =>
            SaturatingAdd(total, Math.Max(0, minutes)));
        int totalPlayTime = checked((int)Math.Min(int.MaxValue, totalMinutes));
        if (game.TotalPlayTime != totalPlayTime)
        {
            game.TotalPlayTime = totalPlayTime;
            changed = true;
        }
        DateTime latestSession = game.PlayTimeSessions
            .Where(session => session.EndedAt >= session.StartedAt)
            .Select(session => session.EndedAt)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        DateTime latestLegacy = game.PlayedTime.Keys
            .Select(Utils.TryParseDateGuessCulture)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        DateTime lastPlayTime = latestSession > latestLegacy ? latestSession : latestLegacy;
        if (game.LastPlayTime != lastPlayTime)
        {
            game.LastPlayTime = lastPlayTime;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// 校正旧数据中逐日汇总小于原生计入时段之和的情况，并刷新分钟兼容层。
    /// 该操作需要遍历全部时段，只应在载入详情或执行显式编辑时调用。
    /// </summary>
    public static bool ReconcileCountedSessionTotals(Galgame game)
    {
        bool changed = EnsureCountedSessionMinimums(game);
        return RefreshDerivedState(game) || changed;
    }

    private static void ApplyIntervalDelta(Galgame game, DateTime start, DateTime end, int sign)
    {
        ApplyIntervalDeltaCore(game, start, end, sign);
        RefreshDerivedState(game);
    }

    private static void ApplyIntervalDeltaCore(Galgame game, DateTime start, DateTime end, int sign)
    {
        if (end <= start) return;
        DateTime cursor = start;
        int safety = 0;
        while (cursor < end && safety++ < 3660)
        {
            DateTime dayEnd = cursor.Date.AddDays(1);
            DateTime segmentEnd = end < dayEnd ? end : dayEnd;
            long seconds = Math.Max(0,
                (long)Math.Round((segmentEnd - cursor).TotalSeconds, MidpointRounding.AwayFromZero));
            if (seconds > 0)
            {
                string key = cursor.Date.ToStringDefault();
                EnsureSecondBucket(game, cursor.Date, key);
                long currentSeconds = Math.Max(0, game.PlayedTimeSeconds[key]);
                game.PlayedTimeSeconds[key] = sign > 0
                    ? SaturatingAdd(currentSeconds, seconds)
                    : Math.Max(0, currentSeconds - seconds);
            }
            cursor = segmentEnd;
        }
    }

    private static void ApplySessionDeltaCore(Galgame game, PlayTimeSession session, int sign)
    {
        foreach (PlayTimeActivityInterval interval in GetEffectiveActivityIntervals(session))
            ApplyIntervalDeltaCore(game, interval.StartedAt, interval.EndedAt, sign);
    }

    private static IReadOnlyList<PlayTimeActivityInterval> GetEffectiveActivityIntervals(
        PlayTimeSession session)
    {
        if (session.ActivityIntervals is not null)
            return session.ActivityIntervals
                .Where(interval => interval.EndedAt > interval.StartedAt)
                .ToArray();
        if (session.EndedAt <= session.StartedAt) return [];
        return
        [
            new PlayTimeActivityInterval
            {
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
            },
        ];
    }

    private static IEnumerable<ActivityDaySlice> SplitActivityByDay(PlayTimeActivityInterval interval)
    {
        DateTime cursor = interval.StartedAt;
        int safety = 0;
        while (cursor < interval.EndedAt && safety++ < 3660)
        {
            DateTime dayEnd = cursor.Date.AddDays(1);
            DateTime segmentEnd = interval.EndedAt < dayEnd ? interval.EndedAt : dayEnd;
            yield return new ActivityDaySlice(
                cursor.Date,
                cursor,
                segmentEnd,
                Math.Max(0, (long)Math.Round(
                    (segmentEnd - cursor).TotalSeconds,
                    MidpointRounding.AwayFromZero)));
            cursor = segmentEnd;
        }
    }

    private static long GetIntervalSeconds(DateTime start, DateTime end)
    {
        if (end <= start) return 0;
        long result = 0;
        DateTime cursor = start;
        int safety = 0;
        while (cursor < end && safety++ < 3660)
        {
            DateTime dayEnd = cursor.Date.AddDays(1);
            DateTime segmentEnd = end < dayEnd ? end : dayEnd;
            result += Math.Max(0,
                (long)Math.Round((segmentEnd - cursor).TotalSeconds, MidpointRounding.AwayFromZero));
            cursor = segmentEnd;
        }
        return result;
    }

    private static bool EnsureCountedSessionMinimums(Galgame game)
    {
        bool changed = false;
        Dictionary<DateTime, long> minimums = [];
        foreach (PlayTimeSession session in game.PlayTimeSessions)
        {
            if (session.Kind == PlayTimeSessionKind.MinuteSampled)
            {
                if (session.SampledMinutesByDay is null) continue;
                foreach ((string key, int minutes) in session.SampledMinutesByDay)
                {
                    DateTime date = Utils.TryParseDateGuessCulture(key);
                    if (date.Year <= 1900 || minutes <= 0) continue;
                    AddMinimum(date.Date, minutes * 60L);
                }
                continue;
            }

            if (!session.CountsTowardPlayTime) continue;
            foreach (PlayTimeDaySegment segment in SplitSessionByDay(session))
                AddMinimum(segment.Date, segment.DurationSeconds);
        }

        foreach ((DateTime date, long minimumSeconds) in minimums)
        {
            string key = date.ToStringDefault();
            if (EnsureSecondBucket(game, date, key)) changed = true;
            long currentSeconds = Math.Max(0, game.PlayedTimeSeconds[key]);
            if (currentSeconds >= minimumSeconds) continue;
            game.PlayedTimeSeconds[key] = minimumSeconds;
            changed = true;
        }
        return changed;

        void AddMinimum(DateTime date, long seconds)
        {
            minimums.TryGetValue(date, out long current);
            minimums[date] = SaturatingAdd(current, Math.Max(0, seconds));
        }
    }

    private static bool EnsureSecondBucket(Galgame game, DateTime date, string key)
    {
        if (game.PlayedTimeSeconds.ContainsKey(key)) return false;
        int legacyMinutes = GetLegacyMinutes(game, date);
        string[] alternateKeys = game.PlayedTime.Keys
            .Where(existingKey => !string.Equals(existingKey, key, StringComparison.Ordinal) &&
                                  Utils.TryParseDateGuessCulture(existingKey).Date == date.Date)
            .ToArray();
        foreach (string alternateKey in alternateKeys) game.PlayedTime.Remove(alternateKey);
        game.PlayedTimeSeconds[key] = Math.Max(0, legacyMinutes * 60L);
        return true;
    }

    private static int GetLegacyMinutes(Galgame game, DateTime date)
    {
        string normalizedKey = date.Date.ToStringDefault();
        if (game.PlayedTime.TryGetValue(normalizedKey, out int direct)) return Math.Max(0, direct);
        int result = 0;
        foreach ((string key, int value) in game.PlayedTime)
        {
            if (Utils.TryParseDateGuessCulture(key).Date == date.Date)
                result = Math.Max(result, Math.Max(0, value));
        }
        return result;
    }

    private static int GetMinuteSamplesForDay(PlayTimeSession session, DateTime date)
    {
        if (session.SampledMinutesByDay is null) return 0;
        long result = 0;
        foreach ((string key, int value) in session.SampledMinutesByDay)
        {
            if (Utils.TryParseDateGuessCulture(key).Date != date.Date) continue;
            result += Math.Max(0, value);
        }
        return checked((int)Math.Min(int.MaxValue, result));
    }

    private static void SetMinuteSamplesForDay(PlayTimeSession session, DateTime date, int minutes)
    {
        session.SampledMinutesByDay ??= new Dictionary<string, int>();
        string[] matchingKeys = session.SampledMinutesByDay.Keys
            .Where(key => Utils.TryParseDateGuessCulture(key).Date == date.Date)
            .ToArray();
        foreach (string key in matchingKeys) session.SampledMinutesByDay.Remove(key);
        if (minutes > 0) session.SampledMinutesByDay[date.Date.ToStringDefault()] = minutes;
    }

    private static void SetDaySeconds(Galgame game, DateTime date, long seconds)
    {
        string[] matchingKeys = game.PlayedTimeSeconds.Keys
            .Where(key => Utils.TryParseDateGuessCulture(key).Date == date.Date)
            .ToArray();
        foreach (string key in matchingKeys) game.PlayedTimeSeconds.Remove(key);
        if (seconds > 0) game.PlayedTimeSeconds[date.Date.ToStringDefault()] = seconds;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}

internal readonly record struct ActivityDaySlice(
    DateTime Date,
    DateTime StartedAt,
    DateTime EndedAt,
    long DurationSeconds);

public readonly record struct PlayTimeDaySegment(
    Guid SessionId,
    DateTime Date,
    DateTime StartedAt,
    DateTime EndedAt,
    long DurationSeconds,
    bool IsOpen,
    PlayTimeSessionKind Kind,
    bool CountsTowardPlayTime);

public readonly record struct MinutePlayTimeDaySegment(
    Guid SessionId,
    DateTime Date,
    DateTime StartedAt,
    DateTime EndedAt,
    int Minutes,
    bool IsOpen,
    bool SpansMultipleDays);

public readonly record struct PlayTimeDayBreakdown(
    long TotalSeconds,
    long PreciseSessionSeconds,
    long MinuteSampleSeconds,
    long UnsegmentedSeconds);
