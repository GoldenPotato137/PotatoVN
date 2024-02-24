using GalgameManager.Helpers;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;
public sealed partial class PvnLoginDialog
{
    public Exception? GetInfoTaskException { get; private set; }
    public PvnLoginType Type { get; private set; }
    public string? UserName { get; private set; }
    public string? Password { get; private set; }
    private PvnServerInfo? _serverInfo;
    
    public PvnLoginDialog(Task<PvnServerInfo?> getInfoTask, string? accountName)
    {
        InitializeComponent();

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        CloseButtonText = "Cancel".GetLocalized();
        DefaultButton = ContentDialogButton.Close;
        UserName = UserNameBox.Text = accountName;

        Loaded += async (_, _) =>
        {
            try
            {
                _serverInfo = await getInfoTask;
            }
            catch (Exception e)
            {
                GetInfoTaskException = e;
            }
            if (_serverInfo is null) 
                Hide();
            // await Task.Delay(500);
            UpdateDisplay();
        };
        PrimaryButtonClick += (_, _) => Type = PvnLoginType.DefaultLogin;
        SecondaryButtonClick += (_, _) => Type = PvnLoginType.DefaultRegister;
        
        UpdateDisplay();
    }

    private void BangumiLogin(object sender, RoutedEventArgs e)
    {
        Type = PvnLoginType.Bangumi;
        Hide();
    }
    
    private void UserNameBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateButton();
        UserName = UserNameBox.Text;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        UpdateButton();
        Password = PasswordBox.Password;
    }

    private void UpdateDisplay()
    {
        if (_serverInfo is null)
        {
            WaitPanel.Visibility = Visibility.Visible;
            UserNamePanel.Visibility = Visibility.Collapsed;
            PasswordPanel.Visibility = Visibility.Collapsed;
            ThirdPartyText.Visibility = Visibility.Collapsed;
            BangumiLoginPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            WaitPanel.Visibility = Visibility.Collapsed;
            UserNamePanel.Visibility = PasswordPanel.Visibility = _serverInfo.DefaultLoginEnable.ToVisibility();
            ThirdPartyText.Visibility = Visibility.Visible;
            BangumiLoginPanel.Visibility = _serverInfo.BangumiLoginEnable.ToVisibility();
            if (_serverInfo.DefaultLoginEnable)
            {
                PrimaryButtonText = "Login".GetLocalized();
                SecondaryButtonText = "Register".GetLocalized();
                UpdateButton();
            }
        }
    }

    private void UpdateButton()
    {
        IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = !string.IsNullOrEmpty(UserNameBox.Text) &&
                                                            !string.IsNullOrEmpty(PasswordBox.Password);
    }
}

public enum PvnLoginType
{
    None,
    DefaultLogin,
    DefaultRegister,
    Bangumi,
}