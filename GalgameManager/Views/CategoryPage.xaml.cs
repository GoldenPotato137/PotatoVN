using GalgameManager.Models;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Views;

public sealed partial class CategoryPage : Page
{
    public CategoryViewModel ViewModel
    {
        get;
    }

    public CategoryPage()
    {
        ViewModel = App.GetService<CategoryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        AutomationProperties.SetAutomationId(CategoryNavView, "CategoryGroupNavigation");
        CategoryNavView.SelectionChanged += CategoryNavView_SelectionChanged;
    }

    private void CategoryGroupItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CategoryGroup group } element) return;

        DependencyObject? parent = element;
        while (parent is not null and not NavigationViewItem)
            parent = VisualTreeHelper.GetParent(parent);
        if (parent is not NavigationViewItem item) return;

        AutomationProperties.SetAutomationId(item, $"CategoryGroup_{(int)group.Type}_{group.Id:N}");
        AutomationProperties.SetName(item, group.Name);
    }

    private void CategoryItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) UpdateCategoryAutomationProperties(button, button.DataContext);
    }

    private void CategoryItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is Button button) UpdateCategoryAutomationProperties(button, args.NewValue);
    }

    private static void UpdateCategoryAutomationProperties(Button button, object? dataContext)
    {
        if (dataContext is not Category category) return;
        AutomationProperties.SetAutomationId(button, $"Category_{category.Id:N}");
        AutomationProperties.SetName(button, category.Name);
    }

    private void CategoryNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not CategoryGroup group) return;
        AutomationProperties.SetAutomationId(CategoryItemsScrollViewer, $"CategoryItems_{group.Id:N}");
        AutomationProperties.SetName(CategoryItemsScrollViewer, group.Name);
    }
}
