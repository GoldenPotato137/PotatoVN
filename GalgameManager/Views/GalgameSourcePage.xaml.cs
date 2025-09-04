using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.Views;

public sealed partial class GalgameSourcePage : Page
{
    public GalgameSourceViewModel ViewModel
    {
        get;
    }

    public GalgameSourcePage()
    {
        ViewModel = App.GetService<GalgameSourceViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (e.NavigationMode == NavigationMode.Back)
        {
            var navigationService = App.GetService<INavigationService>();

            if (ViewModel.Item != null)
            {
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.Item);
            }
        }
    }

    // winui3已知bug：ItemsRepeater无法使用Binding绑定DataContext外的命令，
    // 见：https://github.com/microsoft/microsoft-ui-xaml/issues/560
    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            ViewModel.RemoveDontScanPathCommand.Execute(path);
    }
}
