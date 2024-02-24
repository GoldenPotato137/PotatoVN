using Windows.Storage;
using Windows.Storage.Pickers;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;
public sealed partial class PvnSetAccountDialog
{
    public string? AvatarPath { get; private set; }
    public string? UserDisplayName { get; private set; }
    public bool Canceled { get; private set; } = true;

    public PvnSetAccountDialog(PvnAccount account)
    {
        InitializeComponent();

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        DefaultButton = ContentDialogButton.Secondary;
        PrimaryButtonClick += (_, _) =>
        {
            Canceled = false;
            AvatarPath = AvatarPathBox.Text;
            UserDisplayName = UserDisplayNameBox.Text;
        };
    }

    private async void SetImgButton_OnClick(object sender, RoutedEventArgs e)
    {
        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".bmp");
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file != null)
            AvatarPathBox.Text = file.Path;
    }
}
