using GalgameManager.Models.Sources;
using GalgameManager.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace GalgameManager.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    /// 当前正在被拖动的库/源
    private GalgameSourceBase? _draggedSource;

    private readonly Brush _transparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public LibraryPage()
    {
        ViewModel = App.GetService<LibraryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        AutomationProperties.SetAutomationId(LibraryContentScrollViewer, "LibraryContent");
    }

    private void SourceItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GalgameSourceBase source })
            ViewModel.NavigateToCommand.Execute(source);
    }

    private void SourceItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element) UpdateSourceAutomationProperties(element, element.DataContext);
    }

    private void SourceItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        UpdateSourceAutomationProperties(sender, args.NewValue);
    }

    private static void UpdateSourceAutomationProperties(FrameworkElement element, object? dataContext)
    {
        if (dataContext is not GalgameSourceBase source) return;
        AutomationProperties.SetAutomationId(element, $"LibrarySource_{source.Id:N}");
        AutomationProperties.SetName(element, source.Name);
    }

    private void SourceItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Panel panel && Resources["SourceHoverBrush"] is Brush brush)
            panel.Background = brush;
    }

    private void SourceItem_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Panel panel)
            panel.Background = _transparentBrush;
    }

    private void SourceItem_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: GalgameSourceBase source } fe)
        {
            _draggedSource = source;
            if (fe is Panel panel) panel.Background = _transparentBrush; // 清掉悬停高亮
            args.Data.RequestedOperation = DataPackageOperation.Move;
            args.Data.SetText(source.Id.ToString()); // 让拖拽包非空，确保 Drop 能触发
            ViewModel.EnterCustomFolderSortMode();
        }
        else
        {
            args.Cancel = true;
        }
    }

    private void SourceItem_DragOver(object sender, DragEventArgs e)
    {
        if (_draggedSource is not null)
            e.AcceptedOperation = DataPackageOperation.Move;
    }

    private void SourceItem_Drop(object sender, DragEventArgs e)
    {
        if (_draggedSource is null) return;
        if (sender is FrameworkElement { DataContext: GalgameSourceBase target } fe
            && !ReferenceEquals(target, _draggedSource))
        {
            // 落点在目标卡片右半边则插到其后，否则插到其前
            var insertAfter = e.GetPosition(fe).X > fe.ActualWidth / 2;
            ViewModel.ReorderSource(_draggedSource, target, insertAfter);
        }
        _draggedSource = null;
    }

    // 并不MVVM，但我想不出更好的方案
    private void UIElement_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties? properties = e.GetCurrentPoint(sender as UIElement).Properties;
        if (properties.IsXButton1Pressed)
        {
            ViewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
        else if (properties.IsXButton2Pressed)
        {
            ViewModel.ForwardCommand.Execute(null);
            e.Handled = true;
        }
    }
}
