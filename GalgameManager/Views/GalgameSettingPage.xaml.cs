using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class GalgameSettingPage : Page
{
    public GalgameSettingViewModel ViewModel
    {
        get;
    }

    public GalgameSettingPage()
    {
        ViewModel = App.GetService<GalgameSettingViewModel>();
        InitializeComponent();
    }
}
