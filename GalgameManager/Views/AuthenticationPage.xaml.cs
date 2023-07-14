using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials.UI;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AuthenticationPage : Page
{
    public AuthenticationViewModel ViewModel
    {
        get;
    }

    public AuthenticationPage(AuthenticationViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }

    private async void OnAuthnPageLoaded(object sender, RoutedEventArgs e)
    {
        var success = await ViewModel.StartAuthentication();
        if (success)
        {
            ViewModel.SetContentAsShellPage();
        }
        else
        {
            //不进行身份验证或者身份验证失败次数过多，就关闭应用
            Application.Current.Exit();
        }
    }
}
