using Windows.Foundation.Metadata;
using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

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
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (e.SourcePageType == typeof(HomePage))
        {
            INavigationService navigationService = App.GetService<INavigationService>();

            if (ViewModel.Item != null)
            {
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.Item);
            }
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackConnectedAnimation", DetailedImage);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ConnectedAnimation? imageAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
        if (imageAnimation != null)
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                imageAnimation.Configuration = new GravityConnectedAnimationConfiguration();
            }
            // Connected animation
            imageAnimation.TryStart(DetailedImage);

        }

    }
}
