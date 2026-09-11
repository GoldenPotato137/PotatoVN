using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Views.Dialog;

public sealed partial class EditPlayTimeSessionDialog : ContentDialog
{
    private readonly PlayTimeSession _original;
    private readonly Func<PlayTimeSession, bool>? _overlapsExistingSession;
    public PlayTimeSession? Result { get; private set; }

    public EditPlayTimeSessionDialog(
        PlayTimeSession session,
        Func<PlayTimeSession, bool>? overlapsExistingSession = null)
        : this(session, false, overlapsExistingSession)
    {
    }

    public EditPlayTimeSessionDialog(
        DateTime startedAt,
        DateTime endedAt,
        Func<PlayTimeSession, bool>? overlapsExistingSession = null)
        : this(new PlayTimeSession
        {
            StartedAt = startedAt,
            EndedAt = endedAt,
            IsOpen = false,
            Kind = PlayTimeSessionKind.Manual,
            CountsTowardPlayTime = true,
            ActivityIntervals =
            [
                new PlayTimeActivityInterval { StartedAt = startedAt, EndedAt = endedAt },
            ],
        }, true, overlapsExistingSession)
    {
    }

    private EditPlayTimeSessionDialog(
        PlayTimeSession session,
        bool isNew,
        Func<PlayTimeSession, bool>? overlapsExistingSession)
    {
        ArgumentNullException.ThrowIfNull(session);
        _original = session.Clone();
        _overlapsExistingSession = overlapsExistingSession;
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element
            ? element.RequestedTheme
            : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = (isNew ? "EditPlayTimeSessionDialog_AddTitle" : "EditPlayTimeSessionDialog_Title").GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        CloseButtonText = "Cancel".GetLocalized();
        DefaultButton = ContentDialogButton.Primary;

        StartDatePicker.Date = new DateTimeOffset(_original.StartedAt.Date);
        SetTime(StartHourBox, StartMinuteBox, StartSecondBox, _original.StartedAt);
        EndDatePicker.Date = new DateTimeOffset(_original.EndedAt.Date);
        SetTime(EndHourBox, EndMinuteBox, EndSecondBox, _original.EndedAt);
        PrimaryButtonClick += ValidateAndBuildResult;
    }

    private void ValidateAndBuildResult(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (StartDatePicker.Date is not { } startDate || EndDatePicker.Date is not { } endDate)
        {
            ShowValidation("EditPlayTimeSessionDialog_DateRequired".GetLocalized());
            args.Cancel = true;
            return;
        }

        DateTime start = startDate.LocalDateTime.Date + ReadTime(StartHourBox, StartMinuteBox, StartSecondBox);
        DateTime end = endDate.LocalDateTime.Date + ReadTime(EndHourBox, EndMinuteBox, EndSecondBox);
        if (end <= start)
        {
            ShowValidation("EditPlayTimeSessionDialog_EndBeforeStart".GetLocalized());
            args.Cancel = true;
            return;
        }

        Result = _original.Clone();
        Result.StartedAt = start;
        Result.EndedAt = end;
        Result.IsOpen = false;
        if (start != _original.StartedAt || end != _original.EndedAt)
        {
            Result.ActivityIntervals =
            [
                new PlayTimeActivityInterval { StartedAt = start, EndedAt = end },
            ];
        }
        if (_overlapsExistingSession?.Invoke(Result) == true)
        {
            Result = null;
            ShowValidation("EditPlayTimeSessionDialog_Overlap".GetLocalized());
            args.Cancel = true;
        }
    }

    private static void SetTime(NumberBox hourBox, NumberBox minuteBox, NumberBox secondBox, DateTime value)
    {
        hourBox.Value = value.Hour;
        minuteBox.Value = value.Minute;
        secondBox.Value = value.Second;
    }

    private static TimeSpan ReadTime(NumberBox hourBox, NumberBox minuteBox, NumberBox secondBox)
    {
        int hour = ReadPart(hourBox, 23);
        int minute = ReadPart(minuteBox, 59);
        int second = ReadPart(secondBox, 59);
        return new TimeSpan(hour, minute, second);
    }

    private static int ReadPart(NumberBox box, int maximum)
    {
        if (double.IsNaN(box.Value)) return 0;
        return Math.Clamp((int)box.Value, 0, maximum);
    }

    private void ShowValidation(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
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
