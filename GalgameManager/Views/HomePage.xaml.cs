using System.ComponentModel;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;
using GalgameManager.Views.Prefab;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation;
using Windows.System;
using Windows.UI.Core;

namespace GalgameManager.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }
    private int _rangeAnchorIndex = -1;
    private readonly PointerEventHandler _gridViewItemPointerPressedHandler;
    private int _pendingShiftEndIndex = -1;
    private bool _isApplyingShiftSelection;
    private HashSet<object>? _anchorSelectionSnapshot;

    public HomePage()
    {
        ViewModel = App.GetService<HomeViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        AutomationProperties.SetAutomationId(GridView, "HomeGameGrid");
        ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _gridViewItemPointerPressedHandler = GridViewItem_PointerPressed;
    }

    private void MainGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.IsBatchMode) return;
        if (e.ClickedItem is Galgame galgame)
        {
            ViewModel.ItemClickCommand.Execute(galgame);
        }
    }

    private void MainGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingShiftSelection)
        {
            ViewModel.MainGridViewSelectionChangedCommand.Execute(e);
            UpdateSelectionOpacity(e);
            return;
        }

        if (ViewModel.IsBatchMode && _pendingShiftEndIndex >= 0 && _anchorSelectionSnapshot is not null)
        {
            ApplyShiftRangeToggle();
            return;
        }

        ViewModel.MainGridViewSelectionChangedCommand.Execute(e);
        UpdateSelectionOpacity(e);
    }

    private void MainGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is GridViewItem container)
        {
            if (args.Item is Galgame galgame)
            {
                AutomationProperties.SetAutomationId(container, $"HomeGame_{galgame.Uuid:N}");
                AutomationProperties.SetName(container, galgame.Name.Value ?? string.Empty);
            }
            container.RemoveHandler(UIElement.PointerPressedEvent, _gridViewItemPointerPressedHandler);
            container.AddHandler(UIElement.PointerPressedEvent, _gridViewItemPointerPressedHandler, true);
            ApplyBatchVisualState(container, updateFlyout: true);
        }
    }

    private void BatchSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsBatchMode || GridView.Items.Count == 0) return;
        if (ViewModel.IsAllSelected)
            GridView.SelectedItems.Clear();
        else
            GridView.SelectAll();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.IsBatchMode)) return;
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateBatchModeVisuals);
            return;
        }

        UpdateBatchModeVisuals();
    }

    private void UpdateBatchModeVisuals()
    {
        if (!ViewModel.IsBatchMode && GridView.SelectedItems.Count > 0)
            GridView.SelectedItems.Clear();
        UpdateRealizedContainers(updateFlyout: true);
    }

    private void UpdateSelectionOpacity(SelectionChangedEventArgs e)
    {
        foreach (var item in e.AddedItems)
            UpdateContainerOpacity(item);
        foreach (var item in e.RemovedItems)
            UpdateContainerOpacity(item);
    }

    private void UpdateContainerOpacity(object item)
    {
        if (GridView.ContainerFromItem(item) is GridViewItem container)
            ApplyBatchVisualState(container, updateFlyout: false);
    }

    private void UpdateRealizedContainers(bool updateFlyout)
    {
        if (GridView.ItemsPanelRoot is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is GridViewItem container)
                    ApplyBatchVisualState(container, updateFlyout);
            }

            return;
        }

        foreach (var item in GridView.Items)
            UpdateContainerVisualState(item, updateFlyout);
    }

    private void UpdateContainerVisualState(object item, bool updateFlyout)
    {
        if (GridView.ContainerFromItem(item) is GridViewItem container)
            ApplyBatchVisualState(container, updateFlyout);
    }

    private void ApplyBatchVisualState(GridViewItem container, bool updateFlyout)
    {
        if (updateFlyout)
            UpdateContainerFlyout(container);
        ApplyBatchOpacity(container);
        UpdateSelectionCheckBox(container);
    }

    private void ApplyBatchOpacity(GridViewItem container)
    {
        double targetOpacity = !ViewModel.IsBatchMode || container.IsSelected ? 1 : 0.4;
        if (container.Opacity != targetOpacity)
            container.Opacity = targetOpacity;
    }

    private void UpdateContainerFlyout(GridViewItem container)
    {
        if (container.ContentTemplateRoot is FrameworkElement root &&
            root.FindName("GalgamePrefabRoot") is GalgamePrefab prefab)
        {
            prefab.Flyout = ViewModel.IsBatchMode ? BatchFlyout : GalFlyout;
        }
    }

    private void UpdateSelectionCheckBox(GridViewItem container)
    {
        if (container.ContentTemplateRoot is FrameworkElement root &&
            root.FindName("SelectionCheckBox") is CheckBox checkBox)
        {
            checkBox.IsChecked = container.IsSelected;
        }
    }

    private void SelectionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsBatchMode)
        {
            if (sender is CheckBox checkBox)
                checkBox.IsChecked = false;
            return;
        }

        if (sender is CheckBox { DataContext: Galgame game } checkBoxItem &&
            GridView.ContainerFromItem(game) is GridViewItem container)
        {
            container.IsSelected = checkBoxItem.IsChecked == true;
        }
    }

    private void GridViewItem_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsBatchMode) return;
        if (sender is not GridViewItem container) return;

        var point = e.GetCurrentPoint(container);
        if (!point.Properties.IsLeftButtonPressed) return;

        int currentIndex = GridView.IndexFromContainer(container);
        if (currentIndex < 0) return;

        bool isShiftDown = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down)
            == CoreVirtualKeyStates.Down;
        if (!isShiftDown)
        {
            _rangeAnchorIndex = currentIndex;
            _pendingShiftEndIndex = -1;
            _anchorSelectionSnapshot = GridView.SelectedItems.Cast<object>().ToHashSet();
            return;
        }

        if (_rangeAnchorIndex < 0)
            _rangeAnchorIndex = currentIndex;

        if (_anchorSelectionSnapshot is null)
            _anchorSelectionSnapshot = GridView.SelectedItems.Cast<object>().ToHashSet();
        _pendingShiftEndIndex = currentIndex;
    }

    private void ApplyShiftRangeToggle()
    {
        if (_anchorSelectionSnapshot is null || _pendingShiftEndIndex < 0) return;
        int startIndex = _rangeAnchorIndex;
        int endIndex = _pendingShiftEndIndex;
        HashSet<object> snapshot = _anchorSelectionSnapshot;
        _pendingShiftEndIndex = -1;

        try
        {
            _isApplyingShiftSelection = true;
            GridView.SelectedItems.Clear();
            foreach (var item in snapshot)
                GridView.SelectedItems.Add(item);

            ToggleRangeSelection(startIndex, endIndex, snapshot);
        }
        finally
        {
            _isApplyingShiftSelection = false;
        }
    }

    private void ToggleRangeSelection(int startIndex, int endIndex, HashSet<object> selectionSnapshot)
    {
        int step = startIndex <= endIndex ? 1 : -1;
        for (int i = startIndex; i != endIndex + step; i += step)
        {
            var item = GridView.Items[i];
            if (selectionSnapshot.Contains(item))
                GridView.SelectedItems.Remove(item);
            else
                GridView.SelectedItems.Add(item);
        }
    }

    private void MainGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        try
        {
            ViewModel.EnterCustomSortMode();
        }
        catch (Exception) { /* ignore */ }
    }
    private void MainGridView_DragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
    {
        try
        {
            ViewModel.SaveCustomSortOrder();
        }
        catch (Exception) { /* ignore */ }
    }
}
