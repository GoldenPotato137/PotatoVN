using GalgameManager.ViewModels;

namespace GalgameManager.Views
{
    public sealed partial class PlayedTimePage
    {
        public PlayedTimeViewModel ViewModel
        {
            get;
        }

        public PlayedTimePage()
        {
            ViewModel = App.GetService<PlayedTimeViewModel>();
            InitializeComponent();
            DataContext = ViewModel;
        }

        private void ChangeTimeFormat(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            if (ViewModel.ChangeTimeFormatCommand.CanExecute(null))
                ViewModel.ChangeTimeFormatCommand.Execute(null);
        }

        private void ActivateBarSegment(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not Microsoft.UI.Xaml.FrameworkElement
                {
                    DataContext: PlayTimeBarSegmentViewModelItem segment
                }) return;
            if (segment.ActivateCommand.CanExecute(null))
                segment.ActivateCommand.Execute(null);
        }

        private void RefreshPlayedTime(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ScrollAnchor? anchor = CaptureScrollAnchor();
            double fallbackOffset = PlayedTimeScrollViewer.VerticalOffset;
            if (!ViewModel.RefreshCommand.CanExecute(null)) return;

            ViewModel.RefreshCommand.Execute(null);
            DispatcherQueue.TryEnqueue(() =>
            {
                PlayedTimeScrollViewer.UpdateLayout();
                if (anchor is { } value &&
                    TryGetDayElement(value.Date, out Microsoft.UI.Xaml.FrameworkElement? day) &&
                    day is not null)
                {
                    double currentY = day.TransformToVisual(PlayedTimeScrollViewer)
                        .TransformPoint(new Windows.Foundation.Point()).Y;
                    PlayedTimeScrollViewer.ChangeView(
                        null,
                        Math.Clamp(PlayedTimeScrollViewer.VerticalOffset + currentY - value.ViewportY,
                            0,
                            PlayedTimeScrollViewer.ScrollableHeight),
                        null,
                        true);
                    return;
                }
                PlayedTimeScrollViewer.ChangeView(
                    null,
                    Math.Min(fallbackOffset, PlayedTimeScrollViewer.ScrollableHeight),
                    null,
                    true);
            });
        }

        private ScrollAnchor? CaptureScrollAnchor()
        {
            ScrollAnchor? result = null;
            double closestY = double.MaxValue;
            for (int index = 0; index < ViewModel.Items.Count; index++)
            {
                if (PlayedTimeItemsRepeater.TryGetElement(index) is not Microsoft.UI.Xaml.FrameworkElement element)
                    continue;
                double y = element.TransformToVisual(PlayedTimeScrollViewer)
                    .TransformPoint(new Windows.Foundation.Point()).Y;
                double bottom = y + element.ActualHeight;
                if (bottom <= 0 || y >= PlayedTimeScrollViewer.ViewportHeight) continue;
                double distance = Math.Abs(Math.Max(0, y));
                if (distance >= closestY) continue;
                closestY = distance;
                result = new ScrollAnchor(ViewModel.Items[index].DateValue, y);
            }
            return result;
        }

        private bool TryGetDayElement(DateTime date, out Microsoft.UI.Xaml.FrameworkElement? element)
        {
            for (int index = 0; index < ViewModel.Items.Count; index++)
            {
                if (ViewModel.Items[index].DateValue != date) continue;
                element = PlayedTimeItemsRepeater.TryGetElement(index) as Microsoft.UI.Xaml.FrameworkElement;
                return element is not null;
            }
            element = null;
            return false;
        }

        private readonly record struct ScrollAnchor(DateTime Date, double ViewportY);
    }
}
