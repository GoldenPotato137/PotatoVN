using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class CloseConfirmDialog
{
    public WindowMode Result;
    public bool RememberMe;
    
    public CloseConfirmDialog()
    {
        InitializeComponent();

        XamlRoot = App.MainWindow!.Content.XamlRoot;

        Title = "CloseConfirmDialog_Title".GetLocalized();
        PrimaryButtonText = "CloseConfirmDialog_Exit".GetLocalized();
        SecondaryButtonText = "CloseConfirmDialog_SystemTray".GetLocalized();
        CloseButtonText = "Cancel".GetLocalized();
        
        DefaultButton = ContentDialogButton.Primary;

        PrimaryButtonClick += (_, _) => Result = WindowMode.Close;
        SecondaryButtonClick += (_, _) => Result = WindowMode.SystemTray;
        CloseButtonClick += (_, _) => Result = WindowMode.Normal;
    }
}