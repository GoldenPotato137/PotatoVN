using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class ChangePlayStatusDialog
{
    public ChangePlayStatusDialog()
    {
        InitializeComponent();
        XamlRoot = App.MainWindow.Content.XamlRoot;
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        Title = "ChangePlayStatusDialog_Title".GetLocalized();
        // DefaultButton = ContentDialogButton.Secondary;
    }
}