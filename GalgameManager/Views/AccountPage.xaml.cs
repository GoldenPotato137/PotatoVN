using GalgameManager.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class AccountPage : Page
{
    public AccountViewModel ViewModel
    {
        get;
    }

    public AccountPage()
    {
        ViewModel = App.GetService<AccountViewModel>();
        InitializeComponent();
    }
}
