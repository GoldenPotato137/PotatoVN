using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views.Dialog;

public sealed partial class VndbAuthDialog : ContentDialog
{
    public string Token
    {
        get => (string)GetValue(TokenProperty);
        set => SetValue(TokenProperty, value);
    }
    
    public static readonly DependencyProperty TokenProperty = DependencyProperty.Register(
        nameof(Token),
        typeof(string),
        typeof(VndbAuthDialog),
        new PropertyMetadata("")
    );

    public VndbAuthDialog()
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Title = "VndbAuthDialog_Title".GetLocalized();
        PrimaryButtonText = "Login".GetLocalized();
        CloseButtonText = "Cancel".GetLocalized();
    }
}
