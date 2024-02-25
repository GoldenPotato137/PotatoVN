using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CommunityToolkit.WinUI.UI.Animations;
using GalgameManager.Models;

namespace GalgameManager.Views;

public sealed partial class GalgameCharacterPage : Page
{
    public GalgameCharacterViewModel ViewModel
    {
        get;
    }

    public GalgameCharacterPage()
    {
        ViewModel = App.GetService<GalgameCharacterViewModel>();
        InitializeComponent();
        //由于某种奇怪的Bug，直接在DetailImage处指定animations:Connected.Key=“galgameItem”没有动画效果
        //所以采用这种写法
        this.RegisterElementForConnectedAnimation("galgameCharacter", DetailImage, 
            new []{InfoPanel}); 
        this.AttachAnchorElementForConnectedAnimation(InfoPanel, DetailImage);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (e.SourcePageType == typeof(HomePage))
        {
            var navigationService = App.GetService<INavigationService>();

            if (ViewModel.Character != null)
            {
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.Character);
            }
        }
    }
}
