using Windows.Storage;
using Windows.Storage.Pickers;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace GalgameManager.Views.Dialog;

public sealed partial class AddSourceDialog : ContentDialog
{
    public bool Canceled;
    
    public int SelectItem
    {
        get => (int)GetValue(SelectItemProperty);
        set => SetValue(SelectItemProperty, value);
    }
    
    public static readonly DependencyProperty SelectItemProperty = DependencyProperty.Register(
        nameof(SelectItem),
        typeof(int),
        typeof(AddSourceDialog),
        new PropertyMetadata(0)
    );
    
    public string Path
    {
        get => (string)GetValue(PathProperty);
        set
        {
            IsPrimaryButtonEnabled = !value.IsNullOrEmpty();
            SetValue(PathProperty, value);
        }
    }

    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
        nameof(Path),
        typeof(string),
        typeof(AddSourceDialog),
        new PropertyMetadata("")
    );
    
    public AddSourceDialog()
    {
        this.InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        IsPrimaryButtonEnabled = false;
        DefaultButton = ContentDialogButton.Primary;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Canceled = false;
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Canceled = true;
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        FolderPicker folderPicker = new();
        folderPicker.FileTypeFilter.Add("*");

        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());

        StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            Path = folder.Path;
        }
    }
}
