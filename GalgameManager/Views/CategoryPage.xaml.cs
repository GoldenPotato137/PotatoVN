using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

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
        InitializeComponent();
    }

    private void CategoryGroupFlyout_OnOpening(object? sender, object e)
    {
        ViewModel.UpdateCategoryGroupFlyout(CategoryGroupFlyout);
    }
}
