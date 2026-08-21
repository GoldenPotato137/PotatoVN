using System.ComponentModel;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class PluginStorePage : Page
{
    public PluginStoreViewModel ViewModel
    {
        get;
    }

    public PluginStorePage()
    {
        ViewModel = App.GetService<PluginStoreViewModel>();
        DataContext = ViewModel;
        InitializeComponent();

        // SelectorBar没有ItemsSource，只能手动灌进去；从枚举生成，新增过滤选项时界面自动跟上
        foreach (StorePluginFilter filter in ViewModel.StatusFilters)
            StatusFilterBar.Items.Add(new SelectorBarItem
            {
                Text = filter.GetLocalized(),
                Tag = filter,
                IsSelected = filter == ViewModel.SelectedStatusFilter,
            });
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void StatusFilterBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is StorePluginFilter filter)
            ViewModel.SelectedStatusFilter = filter;
    }

    /// <summary>
    /// 过滤条件也可能从界面之外被改动（如空状态里的重置按钮），这时把SelectorBar的选中项同步过来。<br/>
    /// 反向的同步（用户点SelectorBar）走<see cref="StatusFilterBar_SelectionChanged"/>，
    /// 两边最终都会落到同一个值上，不会来回震荡。
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.SelectedStatusFilter)) return;
        foreach (SelectorBarItem item in StatusFilterBar.Items)
            item.IsSelected = item.Tag is StorePluginFilter filter && filter == ViewModel.SelectedStatusFilter;
    }
}
