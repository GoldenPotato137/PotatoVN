using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CommunityToolkit.WinUI.UI.Animations;
using Microsoft.UI.Xaml;

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
        //由于某种奇怪的Bug，直接在DetailImage处指定animations:Connected.Key=“galgameItem”没有动画效果
        //所以采用这种写法
        this.RegisterElementForConnectedAnimation("galgameItem", DetailImage, 
            new []{InfoPanel}); 
        this.AttachAnchorElementForConnectedAnimation(InfoPanel, DetailImage);



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
