using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Helpers;

namespace GalgameManager.Views.Dialog;

public sealed partial class UnpackDialog
{
    public string? Password;
    public string PackName => PackNameText.Text;
    public string GameName => GameNameText.Text;
    public StorageFile? StorageFile;
        
    public UnpackDialog()
    {
        InitializeComponent();

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        Title = "UnpackDialog_Title".GetLocalized();
        
        SecondaryButtonClick += (_, _) => StorageFile = null;
    }

    public async new Task ShowAsync()
    {
        await GetPack();
        if (StorageFile is null) return;
        await base.ShowAsync();
    }

    [RelayCommand]
    private async Task GetPack()
    {
        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".zip");
        openPicker.FileTypeFilter.Add(".7z");
        openPicker.FileTypeFilter.Add(".tar");
        openPicker.FileTypeFilter.Add(".001");
        StorageFile = await openPicker.PickSingleFileAsync();
            
        PackNameText.Text = StorageFile?.Name ?? string.Empty;
        GameNameText.Text = StorageFile?.DisplayName ?? string.Empty;
    }
}