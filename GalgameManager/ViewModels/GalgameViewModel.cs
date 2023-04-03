using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class GalgameViewModel : ObservableRecipient, INavigationAware
{
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private Galgame? _item;
    public XamlRoot? XamlRoot { get; set; }

    public Galgame? Item
    {
        get => _item;
        private set => SetProperty(ref _item, value);
    }

    private async Task Init(ILocalSettingsService localSettingsService)
    {
        var tmp = await localSettingsService.ReadSettingAsync<string>("AppBackgroundRequestedTheme");
        Console.WriteLine(tmp);
        await Task.CompletedTask;
    }

    public GalgameViewModel(IDataCollectionService<Galgame> dataCollectionService, ILocalSettingsService localSettingsService)
    {
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)dataCollectionService;
        Task.Run(()=> Init(localSettingsService));
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is string name)
        {
            var data = await _dataCollectionService.GetContentGridDataAsync();
            Item = data.First(i => i.Name == name);
            Item.CheckSavePosition();
        }
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private async void Play()
    {
        if (Item == null) return;
        if (Item.ExePath == null)
        {
            var exes = Item.GetExes();
            if (exes.Count == 0)
            {
                var dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "未找到可执行文件",
                    PrimaryButtonText = "确定"
                };
                await dialog.ShowAsync();
            }
            else if (exes.Count == 1)
            {
                Item.ExePath = exes[0];
            }
            else
            {
                var dialog = new FilePickerDialog(XamlRoot!,"选择可执行文件", exes);
                await dialog.ShowAsync();
                if (dialog.SelectedFile != null)
                    Item.ExePath = dialog.SelectedFile;
            }
        }
        
        if (Item.ExePath != null)
        {
            Item.LastPlay = DateTime.Now.ToShortDateString();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Item.ExePath,
                    WorkingDirectory = Item.Path,
                    UseShellExecute = false
                }
            };
            process.Start();
        }
    }

    [RelayCommand]
    private async void GetInfoFromRss()
    {
        if (Item == null) return;
        await _galgameService.PhraseGalInfoAsync(Item);
    }
}


public class FilePickerDialog : ContentDialog
{
    public string? SelectedFile { get; private set; }
    private StackPanel StackPanel { get; set; } = null!;

    public FilePickerDialog(XamlRoot xamlRoot, string title, List<string> files)
    {
        XamlRoot = xamlRoot;
        Title = title;
        Content = CreateContent(files);
        PrimaryButtonText = "确定";
        SecondaryButtonText = "取消";

        IsPrimaryButtonEnabled = false;

        PrimaryButtonClick += (_, _) => { };
        SecondaryButtonClick += (_, _) => { SelectedFile = null; };
    }

    private UIElement CreateContent(List<string> files)
    {
        StackPanel = new StackPanel();
        foreach (var file in files)
        {
            var radioButton = new RadioButton
            {
                Content = file,
                GroupName = "ExeFiles"
            };
        
            radioButton.Checked += RadioButton_Checked;
            StackPanel.Children.Add(radioButton);
        }
        return StackPanel;
    }

    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        var radioButton = (RadioButton)sender;
        SelectedFile = radioButton.Content.ToString()!;
        IsPrimaryButtonEnabled = true;
    }
}
