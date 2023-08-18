using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views.Dialog;

public sealed partial class SelectAuthModeDialog : ContentDialog
{
    public int SelectItem
    {
        get;
        set;
    } = 0;

    public string AccessToken
    {
        get;
        set;
    } = "";

    public SelectAuthModeDialog()
    {
        InitializeComponent();
        XamlRoot = App.MainWindow.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Title = "选择登录方式";
        PrimaryButtonText = "登录";
        CloseButtonText = "Cancel".GetLocalized();
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
    }

    private void RadioButtons_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (SelectItem)
        {
            case 0:
                AccessTokenTextBox.Visibility = Visibility.Collapsed;
                break;
            case 1:
                AccessTokenTextBox.Visibility = Visibility.Visible;
                break;
        }
    }
}
