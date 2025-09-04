using Windows.Storage;
using Windows.Storage.Pickers;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace GalgameManager.Views.Dialog;

public sealed partial class AddSourceDialog : ContentDialog
{
    public bool Canceled = true;
    public GalgameSourceType SelectedType { get; private set; }
    public bool ManualSelectFolder { get; private set; }

    private static readonly GalgameSourceType[] SourceTypes =
    [
        GalgameSourceType.LocalFolder,
        GalgameSourceType.Steam,
    ];
    
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
        InitializeComponent();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        SourceTypeComboBox.ItemsSource = SourceTypes;
        SourceTypeComboBox.SelectedItem = SourceTypes[0]; // 默认选择本地文件夹
        SourceTypeComboBox.SelectedItemChangedEvent += SourceTypeComboBoxOnSelectedItemChangedEvent; 
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content!.XamlRoot;
        IsPrimaryButtonEnabled = false;
        DefaultButton = ContentDialogButton.Primary;
    }

    private void SourceTypeComboBoxOnSelectedItemChangedEvent(object? obj)
    {
        if (obj is not GalgameSourceType type) return; //不应该发生
        SelectedType = type;
        UpdateMsg();
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Canceled = false;
        SelectedType = (GalgameSourceType)SourceTypeComboBox.SelectedItem!;
        ManualSelectFolder = ManualSelectFolderCheckBox.IsChecked ?? false;
    }
    
    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        FolderPicker folderPicker = new();
        folderPicker.FileTypeFilter.Add("*");

        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());

        StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
        if (folder is null) return;
        Path = folder.Path;
        UpdateMsg();
    }

    private void UpdateMsg()
    {
        switch (SelectedType)
        {
            case GalgameSourceType.Steam:
                if (string.IsNullOrEmpty(Path))
                    DisplayMsg(InfoBarSeverity.Informational, "AddSourceDialog_SteamInfo".GetLocalized());
                else if (!Path.Contains("steamapps"))
                {
                    Path = string.Empty;
                    DisplayMsg(InfoBarSeverity.Error, "AddSourceDialog_SteamInfo".GetLocalized());
                }
                else
                    DisplayMsg(InfoBarSeverity.Informational, string.Empty);
                break;
            default:
                DisplayMsg(InfoBarSeverity.Informational, string.Empty);
                break;
        }
        InfoBarLine.Visibility = InfoBar.Visibility;
    }

    /// <summary>
    /// 如果message为empty，则关闭infobar显示
    /// </summary>
    /// <param name="severity"></param>
    /// <param name="message"></param>
    private void DisplayMsg(InfoBarSeverity severity, string message)
    {
        if (string.IsNullOrEmpty(message))
            InfoBar.Visibility = Visibility.Collapsed;
        else
        {
            InfoBar.Visibility = Visibility.Visible;
            InfoBar.Severity = severity;
            InfoBar.Message = message;
        }
    }
}