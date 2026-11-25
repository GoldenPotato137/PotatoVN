using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class DownloadScopeDialog
{
    public bool DownloadStaffs { get; private set; }
    public bool DownloadCharacters { get; private set; }

    public DownloadScopeDialog(bool downloadStaffs, bool downloadCharacters)
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;

        DownloadStaffs = downloadStaffs;
        DownloadCharacters = downloadCharacters;

        StaffCheckBox.IsChecked = downloadStaffs;
        CharacterCheckBox.IsChecked = downloadCharacters;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        DownloadStaffs = StaffCheckBox.IsChecked ?? false;
        DownloadCharacters = CharacterCheckBox.IsChecked ?? false;
    }
}
