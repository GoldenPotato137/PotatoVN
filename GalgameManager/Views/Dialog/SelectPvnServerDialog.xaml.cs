using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class SelectPvnServerDialog
{
    public string? ServerUrl;
    private readonly HttpClient _httpClient = Utils.GetDefaultHttpClient();
    
    public SelectPvnServerDialog()
    {
        InitializeComponent();
        
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "SelectPvnServerDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        IsPrimaryButtonEnabled = false;
        DefaultButton = ContentDialogButton.Primary;
        PrimaryButtonClick += (_, _) =>
        {
            ServerUrl = TextBox.Text;
        };
        SecondaryButtonText = "Cancel".GetLocalized();
        TextBox.PlaceholderText = "SelectPvnServerDialog_Placeholder".GetLocalized();
        Button.Content = "SelectPvnServerDialog_Check".GetLocalized();
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        Button.Content = "SelectPvnServerDialog_Checking".GetLocalized();
        try
        {
            Uri baseUri = new(TextBox.Text);
            HttpResponseMessage response = await _httpClient.GetAsync(new Uri(baseUri, "Server/info"));
            IsPrimaryButtonEnabled = response.IsSuccessStatusCode;
            Button.Content = response.IsSuccessStatusCode ? "OK" : "Failed";
        }
        catch
        {
            Button.Content = "Failed";
            Reset();
        }
    }

    private void TextBox_OnTextChanged(object? sender, TextChangedEventArgs? e)
    {
        Reset();
        Button.Content = "SelectPvnServerDialog_Check".GetLocalized();
    }

    private void Reset()
    {
        IsPrimaryButtonEnabled = false;
        ServerUrl = null;
    }
}