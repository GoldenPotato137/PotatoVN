using Windows.Foundation.Metadata;
using CommunityToolkit.WinUI.UI.Controls;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

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

    private void ListViewBase_OnItemClick(object sender, ItemClickEventArgs e)
    {
        INavigationService navigationService = App.GetService<INavigationService>();
        if (AdaptiveGridView.ContainerFromItem(e.ClickedItem) is GridViewItem container)
        {
            if (container.Content is Galgame clickedItem)
            {
                ConnectedAnimation? animation = AdaptiveGridView.PrepareConnectedAnimation("ForwardConnectedAnimation", clickedItem, "connectedElement");
                navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
                navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, clickedItem.Path, infoOverride:new SuppressNavigationTransitionInfo());
            }
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        INavigationService navigationService = App.GetService<INavigationService>();
        if(navigationService.StoredItem is Galgame storedItem)
        {
            // If the connected item appears outside the viewport, scroll it into view.
            AdaptiveGridView.ScrollIntoView(storedItem, ScrollIntoViewAlignment.Default);
            AdaptiveGridView.UpdateLayout();

            // Play the second connected animation.
            ConnectedAnimation animation =
                ConnectedAnimationService.GetForCurrentView().GetAnimation("BackConnectedAnimation");
            if (animation != null)
            {
                // Setup the "back" configuration if the API is present.
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7)  && e.NavigationMode == NavigationMode.Back)
                {
                    animation.Configuration = new DirectConnectedAnimationConfiguration();
                }

                AdaptiveGridView.TryStartConnectedAnimationAsync(animation, storedItem, "connectedElement").GetResults();
            }

            // Set focus on the list
            AdaptiveGridView.Focus(FocusState.Programmatic);
        }
        
    }
}
