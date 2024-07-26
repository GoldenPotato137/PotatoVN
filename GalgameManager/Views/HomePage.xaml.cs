using CommunityToolkit.WinUI.UI.Controls;
using GalgameManager.Models;
using GalgameManager.ViewModels;
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

    // 不是很mvvm 
    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var item in e.AddedItems)
        {
            if (item is Galgame g)
            {
                ViewModel.SelectedGalgames.Add(g);
            }
        }
        foreach (var item in e.RemovedItems)
        {
            if (item is Galgame g)
            {
                ViewModel.SelectedGalgames.Remove(g);
            }
        }
    }
}
