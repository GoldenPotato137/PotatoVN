using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class HelpPage : Page
{
    public HelpViewModel ViewModel
    {
        get;
    }

    public HelpPage()
    {
        ViewModel = App.GetService<HelpViewModel>();
        InitializeComponent();
    }
}
