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
        new PropertyMetadata("", 
            propertyChangedCallback:new PropertyChangedCallback(PropertyChangedCallback))
    );

    private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d.GetValue(TokenProperty) is string a && Regex.IsMatch(a, @"^[0-9a-z]{4}-[0-9a-z]{5}-[0-9a-z]{5}-[0-9a-z]{4}-[0-9a-z]{5}-[0-9a-z]{5}-[0-9a-z]{4}$"))
        {
            d.SetValue(IsPrimaryButtonEnabledProperty, true);
        }
        else
        {
            d.SetValue(IsPrimaryButtonEnabledProperty, false);
        }
    }

    public VndbAuthDialog()
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Title = "Vndb登录";
        //TODO: 界面优化
        PrimaryButtonText = "Login".GetLocalized();
        CloseButtonText = "Cancel".GetLocalized();
        IsPrimaryButtonEnabled = false;
    }
}
