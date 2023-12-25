using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
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
        ViewModel = App.GetService<HomeViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    //已知bug: https://github.com/microsoft/microsoft-ui-xaml/issues/560
    //临时解决方案，见：https://www.youtube.com/watch?v=vVmtt89G8q8
    private void FilterDeleteButton_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Command is not null) return;
        button.Command = ViewModel.DeleteFilterCommand;
    }
}
