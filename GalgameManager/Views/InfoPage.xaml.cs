using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class InfoPage : Page
{
    public InfoViewModel ViewModel
    {
        get;
    }

    public InfoPage()
    {
        ViewModel = App.GetService<InfoViewModel>();
        InitializeComponent();
    }
}
