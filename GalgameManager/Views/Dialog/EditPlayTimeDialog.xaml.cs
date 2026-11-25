using System.Collections.ObjectModel;
using GalgameManager.Core.Helpers;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Views.Dialog;

public sealed partial class EditPlayTimeDialog : ContentDialog
{
    private readonly ObservableCollection<DisplayPlayTime> _playTimes = new();
    private readonly Galgame _galgame;
    private readonly Dictionary<DateTime, long> _sessionMinimumSeconds;
    private readonly HashSet<DateTime> _originalEditableDates = [];
    private readonly bool _preciseMode;
    private int _playCount;
    public Visibility SecondsVisibility { get; }
    public Visibility PreciseVisibility { get; }
    public Visibility MinuteVisibility { get; }
    public double DialogMinWidth { get; }
    public double DialogMaxHeight { get; }

    public EditPlayTimeDialog(Galgame galgame, bool preciseMode = true)
    {
        _galgame = galgame;
        _preciseMode = preciseMode;
        SecondsVisibility = preciseMode ? Visibility.Visible : Visibility.Collapsed;
        PreciseVisibility = preciseMode ? Visibility.Visible : Visibility.Collapsed;
        MinuteVisibility = preciseMode ? Visibility.Collapsed : Visibility.Visible;
        DialogMinWidth = preciseMode ? 500 : 0;
        DialogMaxHeight = preciseMode ? 420 : 300;
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "EditPlayTimeDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        DescriptionText.Text = (preciseMode
            ? "EditPlayTimeDialog_PreciseDescription"
            : "EditPlayTimeDialog_MinuteDescription").GetLocalized();

        _playCount = _galgame.PlayCount;
        PlayCountNumberBox.Value = _playCount;
        Dictionary<DateTime, long> preciseSessionMinimumSeconds = _galgame.PlayTimeSessions
            .Where(session => session.CountsTowardPlayTime)
            .SelectMany(PlayTimeSessionHelper.SplitSessionByDay)
            .GroupBy(segment => segment.Date)
            .ToDictionary(group => group.Key, group => group.Aggregate(0L, (total, segment) =>
                total > long.MaxValue - segment.DurationSeconds
                    ? long.MaxValue
                    : total + segment.DurationSeconds));

        IEnumerable<string> dateKeys = preciseMode
            ? _galgame.PlayedTime.Keys.Concat(_galgame.PlayedTimeSeconds.Keys)
            : _galgame.PlayedTime.Keys;
        HashSet<DateTime> dates = dateKeys
            .Select(Utils.TryParseDateGuessCulture)
            .Where(date => date.Year > 1900)
            .Select(date => date.Date)
            .ToHashSet();
        foreach (DateTime date in preciseSessionMinimumSeconds.Keys) dates.Add(date);
        foreach (PlayTimeSession session in _galgame.PlayTimeSessions
                     .Where(session => session.Kind == PlayTimeSessionKind.MinuteSampled))
        {
            if (session.SampledMinutesByDay is null) continue;
            foreach (string key in session.SampledMinutesByDay.Keys)
            {
                DateTime date = Utils.TryParseDateGuessCulture(key);
                if (date.Year > 1900) dates.Add(date.Date);
            }
        }

        _sessionMinimumSeconds = dates.ToDictionary(
            date => date,
            date =>
            {
                preciseSessionMinimumSeconds.TryGetValue(date, out long preciseMinimum);
                long minuteMinimum = PlayTimeSessionHelper.GetMinuteSampleSecondsForDay(_galgame, date);
                return preciseMinimum > long.MaxValue - minuteMinimum
                    ? long.MaxValue
                    : preciseMinimum + minuteMinimum;
            });
        _originalEditableDates.UnionWith(dates);

        foreach (DateTime date in dates.OrderBy(date => date))
        {
            _sessionMinimumSeconds.TryGetValue(date, out long minimumSeconds);
            _playTimes.Add(new DisplayPlayTime(
                date.ToStringDefault(),
                PlayTimeSessionHelper.GetDaySeconds(_galgame, date),
                minimumSeconds,
                _preciseMode));
        }
        PreciseListView.ItemsSource = _playTimes;
        MinuteListView.ItemsSource = _playTimes;

        PrimaryButtonClick += ValidateAndApply;
    }

    private void ValidateAndApply(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        DisplayPlayTime? invalid = _playTimes.FirstOrDefault(time => time.TotalSeconds < time.MinimumSeconds);
        if (invalid is not null)
        {
            ValidationMessage.Text = "EditPlayTimeDialog_SessionMinimum".GetLocalized(
                invalid.Date,
                TimeToDisplayTimeConverter.ConvertSeconds(invalid.MinimumSeconds));
            ValidationMessage.Visibility = Visibility.Visible;
            args.Cancel = true;
            return;
        }

        _galgame.PlayedTime = [];
        HashSet<DateTime> retainedDates = _playTimes
            .Select(time => Utils.TryParseDateGuessCulture(time.Date).Date)
            .ToHashSet();

        if (_preciseMode)
        {
            _galgame.PlayedTimeSeconds = [];
        }
        else
        {
            foreach (DateTime removedDate in _originalEditableDates.Except(retainedDates))
                RemoveSecondBuckets(removedDate);
        }
        _galgame.PlayCount = _playCount;
        foreach (DisplayPlayTime time in _playTimes)
        {
            long seconds = time.TotalSeconds;
            DateTime date = Utils.TryParseDateGuessCulture(time.Date).Date;
            RemoveSecondBuckets(date);
            if (seconds <= 0) continue;
            _galgame.PlayedTimeSeconds[date.ToStringDefault()] = seconds;
        }
        PlayTimeSessionHelper.RefreshDerivedState(_galgame);
    }

    private void RemoveSecondBuckets(DateTime date)
    {
        string[] keys = _galgame.PlayedTimeSeconds.Keys
            .Where(key => Utils.TryParseDateGuessCulture(key).Date == date.Date)
            .ToArray();
        foreach (string key in keys) _galgame.PlayedTimeSeconds.Remove(key);
    }

    private void PlayCountNumberBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        _playCount = (int)(double.IsNaN(args.NewValue) ? 0 : args.NewValue);
    }

    private void DatePickerFlyout_OnDatePicked(DatePickerFlyout sender, DatePickedEventArgs args)
    {
        DateTime selectedDate = sender.Date.Date;
        if (_playTimes.Any(time => Utils.TryParseDateGuessCulture(time.Date).Date == selectedDate)) return;
        _sessionMinimumSeconds.TryGetValue(selectedDate, out long minimumSeconds);
        DisplayPlayTime newTime = new(
            selectedDate.ToStringDefault(),
            PlayTimeSessionHelper.GetDaySeconds(_galgame, selectedDate),
            minimumSeconds,
            _preciseMode);
        foreach (DisplayPlayTime time in _playTimes)
            if (newTime < time)
            {
                _playTimes.Insert(_playTimes.IndexOf(time), newTime);
                return;
            }
        _playTimes.Add(newTime);
    }

    private void ButtonDelete_OnClick(object sender, RoutedEventArgs e)
    {
        object? selectedItem = _preciseMode
            ? PreciseListView.SelectedItem
            : MinuteListView.SelectedItem;
        if (selectedItem is not DisplayPlayTime time) return;
        if (time.MinimumSeconds > 0)
        {
            ValidationMessage.Text = "EditPlayTimeDialog_SessionDateCannotDelete".GetLocalized(time.Date);
            ValidationMessage.Visibility = Visibility.Visible;
            return;
        }
        _playTimes.Remove(time);
    }

    private void DialogContentRoot_OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (args.OriginalSource is not DependencyObject source) return;
        for (DependencyObject? current = source;
             current is not null && !ReferenceEquals(current, sender);
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is Microsoft.UI.Xaml.Controls.Control) return;
        }
        Focus(FocusState.Programmatic);
    }
}

public class DisplayPlayTime
{
    public string Date { get; }
    public double PlayedTime { get; set; }
    public double Hours { get; set; }
    public double Minutes { get; set; }
    public double Seconds { get; set; }
    public Visibility SecondsVisibility { get; }
    public long MinimumSeconds { get; }
    private readonly bool _preciseMode;
    private readonly long _preservedSeconds;
    public long TotalSeconds => _preciseMode
        ? ReadPart(Hours, long.MaxValue / 3600) * 3600L +
          ReadPart(Minutes, 59) * 60L +
          ReadPart(Seconds, 59)
        : ReadPart(PlayedTime, int.MaxValue) * 60L + _preservedSeconds;

    public DisplayPlayTime(string date, long totalSeconds, long minimumSeconds, bool preciseMode = true)
    {
        Date = date;
        _preciseMode = preciseMode;
        long normalized = Math.Max(0, totalSeconds);
        PlayedTime = normalized / 60;
        Hours = normalized / 3600;
        Minutes = normalized % 3600 / 60;
        _preservedSeconds = normalized % 60;
        Seconds = preciseMode ? _preservedSeconds : 0;
        SecondsVisibility = preciseMode ? Visibility.Visible : Visibility.Collapsed;
        MinimumSeconds = Math.Max(0, minimumSeconds);
    }

    private static long ReadPart(double value, long maximum)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        return Math.Min((long)value, maximum);
    }

    public static bool operator < (DisplayPlayTime x, DisplayPlayTime y)
    {
        try
        {
            var arrX = x.Date.Split('/');
            var arrY = y.Date.Split('/');
            
            // 确保日期数组有足够的元素
            if (arrX.Length < 3 || arrY.Length < 3)
                return string.Compare(x.Date, y.Date, StringComparison.Ordinal) < 0;
                
            if (int.Parse(arrX[0]) != int.Parse(arrY[0])) return int.Parse(arrX[0]) < int.Parse(arrY[0]);
            if (int.Parse(arrX[1]) != int.Parse(arrY[1])) return int.Parse(arrX[1]) < int.Parse(arrY[1]);
            return int.Parse(arrX[2]) < int.Parse(arrY[2]);
        }
        catch (Exception)
        {
            return string.Compare(x.Date, y.Date, StringComparison.Ordinal) < 0; // 使用字符串比较作为备选方案
        }
    }

    public static bool operator > (DisplayPlayTime x, DisplayPlayTime y)
    {
        return !(x < y);
    }
}
