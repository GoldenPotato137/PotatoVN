using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CommunityToolkit.WinUI.UI.Animations;

namespace GalgameManager.Views;

public sealed partial class HomeDetailPage : Page
{
    public GalgameViewModel ViewModel
    {
        get;
    }

    public HomeDetailPage()
    {
        ViewModel = App.GetService<GalgameViewModel>();
        InitializeComponent();
        this.RegisterElementForConnectedAnimation("galgameItem", DetailImage); 
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (e.SourcePageType == typeof(HomePage))
        {
            var navigationService = App.GetService<INavigationService>();

            if (ViewModel.Item != null)
            {
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.Item);
            }
        }
    }
}
