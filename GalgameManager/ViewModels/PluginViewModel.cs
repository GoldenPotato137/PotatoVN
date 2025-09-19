using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class PluginViewModel(IPluginService pluginService, IInfoService infoService)
    : ObservableRecipient, INavigationAware
{
    public ObservableCollection<PluginX> Plugins = [];

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            Plugins = await pluginService.GetAllPluginsAsync();
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
    }

    public void OnNavigatedFrom() { }

    [RelayCommand]
    private async Task AddPlugin()
    {
        try
        {
            FolderPicker folderPicker = new();
            folderPicker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());
            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
            if (folder is null) return;
            await pluginService.AddPluginAsync(folder.Path);
        }
        catch (Exception e)
        {
            infoService.Info(InfoBarSeverity.Error, "PluginPage_Add_Failed".GetLocalized(),
                e is PvnException pvnE ? pvnE.FullMsg : e.ToString());
        }
    }
}
