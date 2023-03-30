using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel
    {
        get;
    }

    public LibraryPage()
    {
        ViewModel = App.GetService<LibraryViewModel>();
        InitializeComponent();
    }
}
