using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class PlayedTimeViewModel : ObservableObject, INavigationAware
{
    private const double HistoricalSegmentOpacity = 0.42;
    private const double LaunchSegmentOpacity = 0.82;

    public Galgame Game = new();
    public ObservableCollection<PlayTimeDayViewModelItem> Items { get; } = new();
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _dateSortLabel = string.Empty;
    [ObservableProperty] private string _recordingModeLabel = string.Empty;
    [ObservableProperty] private bool _precisePlayTimeEnabled;
    [ObservableProperty] private Visibility _preciseModeVisibility = Visibility.Collapsed;

    private readonly INavigationService _navigationService;
    private readonly IPvnService _pvnService;
    private readonly IInfoService _infoService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IBgTaskService _bgTaskService;
    private readonly GalgameCollectionService _galgameCollectionService;
    private double _totalWidth;
    private bool _precisePlayTime;
    private bool _timeAsHour;
    private bool _dateDescending;

    public PlayedTimeViewModel(
        INavigationService navigationService,
        IGalgameCollectionService gameCollectionService,
        IPvnService pvnService,
        IInfoService infoService,
        ILocalSettingsService localSettingsService,
        IBgTaskService bgTaskService)
    {
        _navigationService = navigationService;
        _galgameCollectionService = (gameCollectionService as GalgameCollectionService)!;
        _pvnService = pvnService;
        _infoService = infoService;
        _localSettingsService = localSettingsService;
        _bgTaskService = bgTaskService;
    }

    public async void OnNavigatedTo(object parameter)
    {
        Debug.Assert(parameter is Galgame);
        if (parameter is not Galgame galgame) return;
        Game = galgame;
        _precisePlayTime = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.PrecisePlayTime);
        UpdateRecordingModeUi();
        _timeAsHour = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.TimeAsHour);
        _dateDescending = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.PlayedTimeDateDescending);
        try
        {
            if (_precisePlayTime && PlayTimeSessionHelper.ReconcileCountedSessionTotals(Game))
                await SaveAndSyncAsync();
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
        Update(preserveExpandedState: false);
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void OnPageSizeChanged(SizeChangedEventArgs e)
    {
        _totalWidth = e.NewSize.Width;
        UpdateWidth();
    }

    private void Update(bool preserveExpandedState = true)
    {
        HashSet<DateTime> expandedDates = preserveExpandedState
            ? Items.Where(item => item.IsExpanded).Select(item => item.DateValue).ToHashSet()
            : [];
        HashSet<(Guid SessionId, DateTime Date)> expandedSessions = preserveExpandedState
            ? Items.SelectMany(item => item.Sessions)
                .Where(item => item.IsExpanded)
                .Select(item => (item.SessionId, item.DateValue))
                .ToHashSet()
            : [];
        List<PlayTimeDayViewModelItem> updatedItems = [];
        Dictionary<Guid, PlayTimeSession> sessionsById = Game.PlayTimeSessions
            .Where(session => session.Kind != PlayTimeSessionKind.MinuteSampled)
            .GroupBy(session => session.Id)
            .ToDictionary(group => group.Key, group => group.First());
        IReadOnlyList<PlayTimeDaySegment> sessionSegments =
            PlayTimeSessionHelper.GetPreciseSessionDaySegments(Game);

        IEnumerable<string> dateKeys = Game.PlayedTime.Keys.Concat(Game.PlayedTimeSeconds.Keys);
        HashSet<DateTime> dates = dateKeys
            .Select(Utils.TryParseDateGuessCulture)
            .Where(date => date.Year > 1900)
            .Select(date => date.Date)
            .ToHashSet();
        if (_precisePlayTime)
            foreach (PlayTimeDaySegment segment in sessionSegments) dates.Add(segment.Date);

        IEnumerable<DateTime> orderedDates = _dateDescending
            ? dates.OrderByDescending(date => date)
            : dates.OrderBy(date => date);
        foreach (DateTime date in orderedDates)
        {
            PlayTimeDaySegment[] daySessions = sessionSegments
                .Where(segment => segment.Date == date)
                .OrderBy(segment => segment.StartedAt)
                .ToArray();
            IReadOnlyList<MinutePlayTimeDaySegment> minuteSegments =
                PlayTimeSessionHelper.GetMinuteSampleSegmentsForDay(Game, date);
            long totalSeconds = PlayTimeSessionHelper.GetDaySeconds(Game, date);
            PlayTimeDayBreakdown breakdown = PlayTimeSessionHelper.GetDayDisplayBreakdown(Game, date);
            long sampledTotal = breakdown.MinuteSampleSeconds;
            long legacySeconds = breakdown.UnsegmentedSeconds;
            bool showSecondPrecision = _precisePlayTime &&
                                       daySessions.Any(segment => segment.CountsTowardPlayTime);

            List<PlayTimeBarSegmentViewModelItem> bars = [];
            if (!_precisePlayTime)
            {
                AddMinuteSampleBars(bars, minuteSegments, sampledTotal);
                AddPreciseSessionBars(bars, daySessions, sessionsById, true);
                if (legacySeconds > 0)
                    bars.Add(new PlayTimeBarSegmentViewModelItem(
                        legacySeconds,
                            HistoricalSegmentOpacity,
                            "PlayedTimePage_MinuteLegacySummary".GetLocalized(
                                TimeToDisplayTimeConverter.ConvertMinuteModeSeconds(
                                    legacySeconds, _timeAsHour)),
                            Edit));
            }
            else
            {
                if (legacySeconds > 0)
                    bars.Add(new PlayTimeBarSegmentViewModelItem(
                        legacySeconds,
                        HistoricalSegmentOpacity,
                        LegacyTextForBar(legacySeconds)));
                AddMinuteSampleBars(bars, minuteSegments, sampledTotal);
                AddPreciseSessionBars(bars, daySessions, sessionsById, false);
            }

            List<PlayTimeSessionViewModelItem> rows = [];
            foreach (PlayTimeDaySegment segment in _precisePlayTime ? daySessions : [])
            {
                if (!sessionsById.TryGetValue(segment.SessionId, out PlayTimeSession? session)) continue;
                rows.Add(new PlayTimeSessionViewModelItem(
                    session,
                    segment,
                    EditSessionAsync,
                    DeleteSessionAsync,
                    CloseSessionAsync,
                    _timeAsHour,
                    expandedSessions.Contains((segment.SessionId, segment.Date))));
            }

            updatedItems.Add(new PlayTimeDayViewModelItem(
                date,
                totalSeconds,
                legacySeconds,
                bars,
                rows,
                _precisePlayTime,
                showSecondPrecision,
                _timeAsHour,
                expandedDates.Contains(date)));
        }

        if (preserveExpandedState)
            SynchronizeItems(updatedItems);
        else
        {
            Items.Clear();
            foreach (PlayTimeDayViewModelItem item in updatedItems) Items.Add(item);
        }

        Summary = Items.Count == 0
            ? "PlayedTimePage_NoSessions".GetLocalized()
            : _precisePlayTime
                ? "PlayedTimePage_PreciseSummary".GetLocalized(
                    Items.Count,
                    Game.PlayTimeSessions.Count,
                    TimeToDisplayTimeConverter.ConvertSeconds(
                        PlayTimeSessionHelper.GetTotalSeconds(Game), _timeAsHour))
                : "PlayedTimePage_MinuteSummary".GetLocalized(Items.Count);
        DateSortLabel = (_dateDescending
            ? "PlayedTimePage_DateSortNewestFirst"
            : "PlayedTimePage_DateSortOldestFirst").GetLocalized();
        UpdateWidth();
    }

    private void SynchronizeItems(IReadOnlyList<PlayTimeDayViewModelItem> updatedItems)
    {
        for (int targetIndex = 0; targetIndex < updatedItems.Count; targetIndex++)
        {
            PlayTimeDayViewModelItem updated = updatedItems[targetIndex];
            int existingIndex = -1;
            for (int index = targetIndex; index < Items.Count; index++)
            {
                if (Items[index].DateValue != updated.DateValue) continue;
                existingIndex = index;
                break;
            }

            if (existingIndex < 0)
            {
                Items.Insert(targetIndex, updated);
                continue;
            }

            if (existingIndex != targetIndex) Items.Move(existingIndex, targetIndex);
            Items[targetIndex].ApplySnapshot(updated);
        }

        while (Items.Count > updatedItems.Count) Items.RemoveAt(Items.Count - 1);
    }

    private void AddMinuteSampleBars(
        List<PlayTimeBarSegmentViewModelItem> bars,
        IReadOnlyList<MinutePlayTimeDaySegment> minuteSegments,
        long visibleTotal)
    {
        long remainingSampled = visibleTotal;
        int minuteIndex = 0;
        foreach (MinutePlayTimeDaySegment segment in minuteSegments)
        {
            minuteIndex++;
            long visibleDuration = Math.Min(segment.Minutes * 60L, remainingSampled);
            if (visibleDuration <= 0) continue;

            string duration = FormatMinuteSegmentDuration(segment.Minutes);
            string toolTip = segment.SpansMultipleDays
                ? segment.IsOpen
                    ? "PlayedTimePage_CrossDayMinuteSegmentOpenToolTip".GetLocalized(
                        segment.Date.ToStringDefault(), duration)
                    : "PlayedTimePage_CrossDayMinuteSegmentToolTip".GetLocalized(
                        segment.Date.ToStringDefault(), duration)
                : segment.IsOpen
                    ? "PlayedTimePage_MinuteSegmentOpenToolTip".GetLocalized(duration)
                    : "PlayedTimePage_MinuteSegmentToolTip".GetLocalized(minuteIndex, duration);
            Func<Task> activate = segment.IsOpen
                ? () => CloseMinuteSessionAsync(segment)
                : () => EditMinuteSessionAsync(segment);
            bars.Add(new PlayTimeBarSegmentViewModelItem(
                visibleDuration,
                LaunchSegmentOpacity,
                toolTip,
                activate));
            remainingSampled -= visibleDuration;
        }
    }

    private void AddPreciseSessionBars(
        List<PlayTimeBarSegmentViewModelItem> bars,
        IEnumerable<PlayTimeDaySegment> daySessions,
        IReadOnlyDictionary<Guid, PlayTimeSession> sessionsById,
        bool interactive)
    {
        foreach (PlayTimeDaySegment segment in daySessions.Where(segment => segment.CountsTowardPlayTime))
        {
            if (!interactive)
            {
                bars.Add(new PlayTimeBarSegmentViewModelItem(
                    segment.DurationSeconds,
                    LaunchSegmentOpacity,
                    TimeToDisplayTimeConverter.ConvertSeconds(segment.DurationSeconds, _timeAsHour)));
                continue;
            }

            if (!sessionsById.TryGetValue(segment.SessionId, out PlayTimeSession? session)) continue;
            string duration = TimeToDisplayTimeConverter.ConvertMinuteModeSeconds(
                segment.DurationSeconds, _timeAsHour);
            string toolTip = (session.IsOpen
                    ? "PlayedTimePage_PreciseSegmentMinuteOpenToolTip"
                    : "PlayedTimePage_PreciseSegmentMinuteToolTip")
                .GetLocalized(duration);
            Func<Task> activate = session.IsOpen
                ? () => CloseSessionAsync(session)
                : () => EditSessionAsync(session);
            bars.Add(new PlayTimeBarSegmentViewModelItem(
                segment.DurationSeconds,
                LaunchSegmentOpacity,
                toolTip,
                activate));
        }
    }

    private string LegacyTextForBar(long seconds) =>
        "PlayedTimePage_LegacySummary".GetLocalized(
            TimeToDisplayTimeConverter.ConvertWholeMinuteSecondsWithUnits(seconds, _timeAsHour));

    private string FormatMinuteSegmentDuration(int minutes) =>
        _precisePlayTime
            ? TimeToDisplayTimeConverter.ConvertWholeMinutesWithUnits(minutes, _timeAsHour)
            : TimeToDisplayTimeConverter.ConvertMinutes(minutes, _timeAsHour);

    private void UpdateRecordingModeUi()
    {
        PrecisePlayTimeEnabled = _precisePlayTime;
        PreciseModeVisibility = _precisePlayTime ? Visibility.Visible : Visibility.Collapsed;
        RecordingModeLabel = (_precisePlayTime
            ? "PlayedTimePage_RecordingModePrecise"
            : "PlayedTimePage_RecordingModeMinute").GetLocalized();
    }

    private void UpdateWidth()
    {
        long maxPlayTime = Items.Count > 0 ? Math.Max(1, Items.Max(item => item.TotalSeconds)) : 1;
        foreach (PlayTimeDayViewModelItem item in Items)
            item.UpdateWidth(_totalWidth, maxPlayTime);
    }

    [RelayCommand]
    private void Back() => _navigationService.GoBack();

    [RelayCommand]
    private void Refresh() => Update();

    [RelayCommand]
    private async Task ChangeTimeFormat()
    {
        _timeAsHour = !_timeAsHour;
        await _localSettingsService.SaveSettingAsync(KeyValues.TimeAsHour, _timeAsHour);
        Game.RaisePropertyChanged(nameof(Game.TotalPlayTime));
        Update();
    }

    [RelayCommand]
    private async Task ToggleDateSort()
    {
        _dateDescending = !_dateDescending;
        await _localSettingsService.SaveSettingAsync(KeyValues.PlayedTimeDateDescending, _dateDescending);
        Update();
    }

    [RelayCommand]
    private async Task ToggleRecordingMode()
    {
        bool nextMode = !_precisePlayTime;
        PlayTimeMutationBackup? backup = nextMode ? PlayTimeMutationBackup.Create(Game) : null;
        try
        {
            await _localSettingsService.SaveSettingAsync(KeyValues.PrecisePlayTime, nextMode);
            _precisePlayTime = nextMode;
            UpdateRecordingModeUi();
            if (_precisePlayTime && PlayTimeSessionHelper.ReconcileCountedSessionTotals(Game))
                await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup?.Restore(Game);
            UpdateRecordingModeUi();
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    [RelayCommand]
    private async Task AddSession()
    {
        if (!_precisePlayTime) return;
        DateTime endedAt = DateTime.Now;
        DateTime startedAt = endedAt.AddMinutes(-1);
        EditPlayTimeSessionDialog dialog = new(
            startedAt,
            endedAt,
            candidate => PlayTimeSessionHelper.HasOverlappingSession(Game, candidate));
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Result is not { } session) return;

        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        try
        {
            PlayTimeSessionHelper.AddSession(Game, session);
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    [RelayCommand]
    private async Task Edit()
    {
        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        ContentDialogResult result = await new EditPlayTimeDialog(Game, _precisePlayTime).ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        try
        {
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    private async Task EditSessionAsync(PlayTimeSession session)
    {
        if (session.IsOpen)
        {
            _infoService.Info(InfoBarSeverity.Warning, "PlayedTimePage_OpenSessionCannotEdit".GetLocalized());
            return;
        }

        EditPlayTimeSessionDialog dialog = new(
            session,
            candidate => PlayTimeSessionHelper.HasOverlappingSession(Game, candidate));
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Result is not { } replacement) return;

        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        try
        {
            PlayTimeSessionHelper.ReplaceSession(Game, session, replacement);
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    private async Task EditMinuteSessionAsync(MinutePlayTimeDaySegment segment)
    {
        if (segment.IsOpen)
        {
            _infoService.Info(InfoBarSeverity.Warning, "PlayedTimePage_OpenSessionCannotEdit".GetLocalized());
            return;
        }

        NumberBox minuteBox = new()
        {
            Value = segment.Minutes,
            Minimum = 1,
            Maximum = int.MaxValue,
            SmallChange = 1,
            LargeChange = 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 150,
        };
        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = (segment.SpansMultipleDays
                    ? "PlayedTimePage_EditCrossDayMinuteSessionDescription"
                    : "PlayedTimePage_EditMinuteSessionDescription")
                .GetLocalized(segment.Date.ToStringDefault()),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(minuteBox);
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element
                ? element.RequestedTheme
                : ElementTheme.Default,
            Title = "PlayedTimePage_EditMinuteSessionTitle".GetLocalized(),
            Content = content,
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "PlayedTimePage_DeleteMinuteSessionButton".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };
        ContentDialogResult dialogResult = await dialog.ShowAsync();
        if (dialogResult == ContentDialogResult.Secondary)
        {
            await DeleteMinuteSessionAsync(segment);
            return;
        }
        if (dialogResult != ContentDialogResult.Primary) return;

        int minutes = double.IsNaN(minuteBox.Value)
            ? segment.Minutes
            : checked((int)Math.Min(int.MaxValue, Math.Max(1, minuteBox.Value)));
        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        try
        {
            if (!PlayTimeSessionHelper.ReplaceMinuteSampleSegment(
                    Game, segment.SessionId, segment.Date, minutes))
            {
                _infoService.Info(InfoBarSeverity.Warning,
                    "PlayedTimePage_OpenSessionCannotEdit".GetLocalized());
                return;
            }
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    private async Task DeleteMinuteSessionAsync(MinutePlayTimeDaySegment segment)
    {
        if (segment.IsOpen)
        {
            _infoService.Info(InfoBarSeverity.Warning, "PlayedTimePage_OpenSessionCannotEdit".GetLocalized());
            return;
        }

        ContentDialog confirm = CreateConfirmationDialog(
            "PlayedTimePage_DeleteMinuteSessionTitle".GetLocalized(),
            (segment.SpansMultipleDays
                    ? "PlayedTimePage_DeleteCrossDayMinuteSessionMessage"
                    : "PlayedTimePage_DeleteMinuteSessionMessage")
                .GetLocalized(
                    segment.Date.ToStringDefault(),
                    FormatMinuteSegmentDuration(segment.Minutes)));
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        try
        {
            if (!PlayTimeSessionHelper.ReplaceMinuteSampleSegment(
                    Game, segment.SessionId, segment.Date, 0)) return;
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    private async Task CloseMinuteSessionAsync(MinutePlayTimeDaySegment segment)
    {
        RecordPlayTimeTask? activeTask = _bgTaskService.GetBgTask<RecordPlayTimeTask>(Game.Uuid.ToString("D"));
        if (activeTask?.ActiveMinuteSessionId == segment.SessionId)
        {
            _infoService.Info(InfoBarSeverity.Warning, "PlayedTimePage_ActiveSessionCannotClose".GetLocalized());
            return;
        }

        ContentDialog confirm = CreateConfirmationDialog(
            "PlayedTimePage_CloseSessionTitle".GetLocalized(),
            "PlayedTimePage_CloseSessionMessage".GetLocalized(segment.EndedAt));
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        try
        {
            if (!PlayTimeSessionHelper.CloseOpenSession(Game, segment.SessionId)) return;
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    private async Task DeleteSessionAsync(PlayTimeSession session)
    {
        if (session.IsOpen)
        {
            _infoService.Info(InfoBarSeverity.Warning, "PlayedTimePage_OpenSessionCannotEdit".GetLocalized());
            return;
        }

        ContentDialog confirm = CreateConfirmationDialog(
            "PlayedTimePage_DeleteSessionTitle".GetLocalized(),
            "PlayedTimePage_DeleteSessionMessage".GetLocalized(
                session.StartedAt,
                session.EndedAt,
                TimeToDisplayTimeConverter.ConvertSeconds(
                    PlayTimeSessionHelper.GetSessionDurationSeconds(session), _timeAsHour)));
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        try
        {
            if (!PlayTimeSessionHelper.DeleteSession(Game, session.Id)) return;
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    private async Task CloseSessionAsync(PlayTimeSession session)
    {
        RecordPlayTimeTask? activeTask = _bgTaskService.GetBgTask<RecordPlayTimeTask>(Game.Uuid.ToString("D"));
        if (activeTask?.ActiveSessionId == session.Id)
        {
            _infoService.Info(InfoBarSeverity.Warning, "PlayedTimePage_ActiveSessionCannotClose".GetLocalized());
            return;
        }

        ContentDialog confirm = CreateConfirmationDialog(
            "PlayedTimePage_CloseSessionTitle".GetLocalized(),
            "PlayedTimePage_CloseSessionMessage".GetLocalized(session.EndedAt));
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        PlayTimeMutationBackup backup = PlayTimeMutationBackup.Create(Game);
        try
        {
            if (!PlayTimeSessionHelper.CloseOpenSession(Game, session.Id)) return;
            await SaveAndSyncAsync();
            Update();
        }
        catch (Exception ex)
        {
            backup.Restore(Game);
            _infoService.Info(InfoBarSeverity.Error,
                "PlayedTimePage_SaveFailed".GetLocalized(), ex.GetBaseException().Message);
        }
    }

    private ContentDialog CreateConfirmationDialog(string title, string message) => new()
    {
        XamlRoot = App.MainWindow!.Content.XamlRoot,
        RequestedTheme = App.MainWindow.Content is FrameworkElement element
            ? element.RequestedTheme
            : ElementTheme.Default,
        Title = title,
        Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
        PrimaryButtonText = "Yes".GetLocalized(),
        CloseButtonText = "Cancel".GetLocalized(),
        DefaultButton = ContentDialogButton.Close,
    };

    private async Task SaveAndSyncAsync()
    {
        PlayTimeSessionHelper.RefreshDerivedState(Game);
        await _galgameCollectionService.SaveGalgameAsync(Game);
        Game.RaisePropertyChanged(nameof(Game.TotalPlayTime));
        Game.RaisePropertyChanged(nameof(Game.LastPlayTime));
        _pvnService.Upload(Game, PvnUploadProperties.PlayTime);
    }

    private sealed class PlayTimeMutationBackup
    {
        private Dictionary<string, int> PlayedTime { get; init; } = [];
        private Dictionary<string, long> PlayedTimeSeconds { get; init; } = [];
        private List<PlayTimeSession> Sessions { get; init; } = [];
        private int TotalPlayTime { get; init; }
        private DateTime LastPlayTime { get; init; }
        private int PlayCount { get; init; }

        internal static PlayTimeMutationBackup Create(Galgame game) => new()
        {
            PlayedTime = new Dictionary<string, int>(game.PlayedTime),
            PlayedTimeSeconds = new Dictionary<string, long>(game.PlayedTimeSeconds),
            Sessions = game.PlayTimeSessions.Select(session => session.Clone()).ToList(),
            TotalPlayTime = game.TotalPlayTime,
            LastPlayTime = game.LastPlayTime,
            PlayCount = game.PlayCount,
        };

        internal void Restore(Galgame game)
        {
            game.PlayedTime = new Dictionary<string, int>(PlayedTime);
            game.PlayedTimeSeconds = new Dictionary<string, long>(PlayedTimeSeconds);
            game.PlayTimeSessions = Sessions.Select(session => session.Clone()).ToList();
            game.TotalPlayTime = TotalPlayTime;
            game.LastPlayTime = LastPlayTime;
            game.PlayCount = PlayCount;
        }
    }
}

public partial class PlayTimeDayViewModelItem : ObservableObject
{
    [ObservableProperty] private double _width;
    public DateTime DateValue { get; }
    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private long _totalSeconds;
    [ObservableProperty] private long _legacySeconds;
    [ObservableProperty] private string _totalText = string.Empty;
    [ObservableProperty] private string _legacyText = string.Empty;
    public ObservableCollection<PlayTimeBarSegmentViewModelItem> Segments { get; }
    public ObservableCollection<PlayTimeSessionViewModelItem> Sessions { get; }
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private Visibility _preciseVisibility;
    [ObservableProperty] private Visibility _minuteVisibility;

    public PlayTimeDayViewModelItem(
        DateTime date,
        long totalSeconds,
        long legacySeconds,
        IEnumerable<PlayTimeBarSegmentViewModelItem> segments,
        IEnumerable<PlayTimeSessionViewModelItem> sessions,
        bool precise,
        bool showSecondPrecision,
        bool timeAsHour,
        bool isExpanded = false)
    {
        DateValue = date;
        Date = precise ? date.ToString("yyyy/M/d dddd") : date.ToString("yyyy/M/d");
        TotalSeconds = Math.Max(0, totalSeconds);
        LegacySeconds = Math.Max(0, legacySeconds);
        TotalText = precise && showSecondPrecision
            ? TimeToDisplayTimeConverter.ConvertSeconds(TotalSeconds, timeAsHour)
            : precise
                ? TimeToDisplayTimeConverter.ConvertWholeMinutesWithUnits(TotalSeconds / 60, timeAsHour)
                : TimeToDisplayTimeConverter.ConvertMinuteModeSeconds(TotalSeconds, timeAsHour);
        LegacyText = LegacySeconds > 0
            ? "PlayedTimePage_LegacySummary".GetLocalized(
                TimeToDisplayTimeConverter.ConvertWholeMinuteSecondsWithUnits(LegacySeconds, timeAsHour))
            : string.Empty;
        Segments = new ObservableCollection<PlayTimeBarSegmentViewModelItem>(segments);
        Sessions = new ObservableCollection<PlayTimeSessionViewModelItem>(sessions);
        PreciseVisibility = precise ? Visibility.Visible : Visibility.Collapsed;
        MinuteVisibility = precise ? Visibility.Collapsed : Visibility.Visible;
        // 首次进入页面时日期默认折叠；刷新或重建列表时可恢复用户当前展开的日期。
        IsExpanded = isExpanded;
    }

    public void ApplySnapshot(PlayTimeDayViewModelItem source)
    {
        Date = source.Date;
        TotalSeconds = source.TotalSeconds;
        LegacySeconds = source.LegacySeconds;
        TotalText = source.TotalText;
        LegacyText = source.LegacyText;
        PreciseVisibility = source.PreciseVisibility;
        MinuteVisibility = source.MinuteVisibility;

        Segments.Clear();
        foreach (PlayTimeBarSegmentViewModelItem segment in source.Segments) Segments.Add(segment);
        SynchronizeSessions(source.Sessions);
    }

    private void SynchronizeSessions(IReadOnlyList<PlayTimeSessionViewModelItem> updatedSessions)
    {
        for (int targetIndex = 0; targetIndex < updatedSessions.Count; targetIndex++)
        {
            PlayTimeSessionViewModelItem updated = updatedSessions[targetIndex];
            int existingIndex = -1;
            for (int index = targetIndex; index < Sessions.Count; index++)
            {
                if (Sessions[index].SessionId != updated.SessionId ||
                    Sessions[index].DateValue != updated.DateValue) continue;
                existingIndex = index;
                break;
            }

            if (existingIndex < 0)
            {
                Sessions.Insert(targetIndex, updated);
                continue;
            }

            if (existingIndex != targetIndex) Sessions.Move(existingIndex, targetIndex);
            PlayTimeSessionViewModelItem existing = Sessions[targetIndex];
            if (ReferenceEquals(existing.SourceSession, updated.SourceSession))
                existing.ApplySnapshot(updated);
            else
            {
                updated.IsExpanded = existing.IsExpanded;
                Sessions[targetIndex] = updated;
            }
        }

        while (Sessions.Count > updatedSessions.Count) Sessions.RemoveAt(Sessions.Count - 1);
    }

    public void UpdateWidth(double totalWidth, long maxPlayTime)
    {
        double reservedWidth = PreciseVisibility == Visibility.Visible ? 260 : 200;
        Width = Math.Max(totalWidth - reservedWidth, 0) * TotalSeconds / Math.Max(1, maxPlayTime);
        long segmentTotal = Math.Max(1, Segments.Aggregate(0L, (total, segment) =>
            total > long.MaxValue - segment.DurationSeconds
                ? long.MaxValue
                : total + segment.DurationSeconds));
        foreach (PlayTimeBarSegmentViewModelItem segment in Segments)
            segment.Width = Math.Max(2, Width * segment.DurationSeconds / segmentTotal);
    }
}

public partial class PlayTimeBarSegmentViewModelItem(
    long durationSeconds,
    double opacity,
    string toolTip = "",
    Func<Task>? activate = null) : ObservableObject
{
    public long DurationSeconds { get; } = Math.Max(0, durationSeconds);
    public double Opacity { get; } = opacity;
    public string ToolTip { get; } = toolTip;
    public Visibility InteractiveVisibility { get; } = activate is null
        ? Visibility.Collapsed
        : Visibility.Visible;
    public IAsyncRelayCommand ActivateCommand { get; } = new AsyncRelayCommand(
        activate ?? (() => Task.CompletedTask));
    [ObservableProperty] private double _width;
}

public partial class PlayTimeSessionViewModelItem : ObservableObject
{
    public Guid SessionId { get; }
    public DateTime DateValue { get; }
    internal PlayTimeSession SourceSession { get; }
    [ObservableProperty] private string _timeRange = string.Empty;
    [ObservableProperty] private string _duration = string.Empty;
    [ObservableProperty] private string _kind = string.Empty;
    [ObservableProperty] private Visibility _editVisibility;
    [ObservableProperty] private Visibility _deleteVisibility;
    [ObservableProperty] private Visibility _closeVisibility;
    [ObservableProperty] private Visibility _emptyActivityVisibility;
    public ObservableCollection<PlayTimeActivityViewModelItem> ActivityIntervals { get; }
    public IAsyncRelayCommand EditCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand CloseCommand { get; }
    [ObservableProperty] private bool _isExpanded;

    public PlayTimeSessionViewModelItem(
        PlayTimeSession session,
        PlayTimeDaySegment segment,
        Func<PlayTimeSession, Task> edit,
        Func<PlayTimeSession, Task> delete,
        Func<PlayTimeSession, Task> close,
        bool timeAsHour,
        bool isExpanded = false)
    {
        SessionId = segment.SessionId;
        DateValue = segment.Date;
        SourceSession = session;
        TimeRange = $"{FormatBoundary(segment.StartedAt, segment.Date, false)}–" +
                    $"{FormatBoundary(segment.EndedAt, segment.Date, true)}";
        Duration = TimeToDisplayTimeConverter.ConvertSeconds(segment.DurationSeconds, timeAsHour);
        Kind = session.IsOpen
            ? "PlayedTimePage_SessionOpen".GetLocalized()
            : session.Kind == PlayTimeSessionKind.Imported
                ? "PlayedTimePage_SessionImported".GetLocalized()
                : session.Kind == PlayTimeSessionKind.Manual
                    ? "PlayedTimePage_SessionManual".GetLocalized()
                    : "PlayedTimePage_SessionNative".GetLocalized();
        EditVisibility = session.IsOpen ? Visibility.Collapsed : Visibility.Visible;
        DeleteVisibility = EditVisibility;
        CloseVisibility = session.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        ActivityIntervals = new ObservableCollection<PlayTimeActivityViewModelItem>(
            PlayTimeSessionHelper.GetActivityIntervalsForDay(session, segment.Date)
                .Select(interval => new PlayTimeActivityViewModelItem(interval, segment.Date, timeAsHour)));
        EmptyActivityVisibility = ActivityIntervals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EditCommand = new AsyncRelayCommand(() => edit(session));
        DeleteCommand = new AsyncRelayCommand(() => delete(session));
        CloseCommand = new AsyncRelayCommand(() => close(session));
        IsExpanded = isExpanded;
    }

    public void ApplySnapshot(PlayTimeSessionViewModelItem source)
    {
        TimeRange = source.TimeRange;
        Duration = source.Duration;
        Kind = source.Kind;
        EditVisibility = source.EditVisibility;
        DeleteVisibility = source.DeleteVisibility;
        CloseVisibility = source.CloseVisibility;
        EmptyActivityVisibility = source.EmptyActivityVisibility;
        ActivityIntervals.Clear();
        foreach (PlayTimeActivityViewModelItem interval in source.ActivityIntervals)
            ActivityIntervals.Add(interval);
    }

    internal static string FormatBoundary(DateTime value, DateTime date, bool isEnd) =>
        isEnd && value == date.AddDays(1) ? "24:00:00" : value.ToString("HH:mm:ss");
}

public sealed class PlayTimeActivityViewModelItem
{
    public string TimeRange { get; }
    public string Duration { get; }

    public PlayTimeActivityViewModelItem(
        PlayTimeActivityInterval interval,
        DateTime date,
        bool timeAsHour)
    {
        TimeRange = $"{PlayTimeSessionViewModelItem.FormatBoundary(interval.StartedAt, date, false)}–" +
                    $"{PlayTimeSessionViewModelItem.FormatBoundary(interval.EndedAt, date, true)}";
        long seconds = Math.Max(0, (long)Math.Round(
            (interval.EndedAt - interval.StartedAt).TotalSeconds,
            MidpointRounding.AwayFromZero));
        Duration = TimeToDisplayTimeConverter.ConvertSeconds(seconds, timeAsHour);
    }
}
