using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Views;
using Microsoft.UI.Xaml.Controls;
using Windows.Security.Credentials.UI;
using Windows.System;

namespace GalgameManager.ViewModels;

public partial class AuthenticationViewModel : ObservableRecipient
{
    public void SetContentAsShellPage()
    {
        ShellPage _shell = App.GetService<ShellPage>();
        App.MainWindow!.Content = _shell;
    }

    public async Task<bool> StartAuthentication()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        var consentResult = await UserConsentVerifierInterop.RequestVerificationForWindowAsync(hwnd, "AuthenticateUserMessage".GetLocalized());
        if (consentResult == UserConsentVerificationResult.Verified)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
