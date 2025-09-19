using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class PluginPage : Page
{
    public PluginViewModel ViewModel
    {
        get;
    }

    public PluginPage()
    {
        ViewModel = App.GetService<PluginViewModel>();
        InitializeComponent();
    }
}
