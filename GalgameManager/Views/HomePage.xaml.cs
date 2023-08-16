using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel
    {
        get;
    }

    public HomePage()
    {
        InitializeComponent();
        ViewModel = App.GetService<HomeViewModel>();
        DataContext = ViewModel;
    }
}
