using System.Collections.ObjectModel;
using System.Windows.Input;

using Windows.Storage.Pickers;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;

using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class HomeViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

    public ICommand ItemClickCommand
    {
        get;
    }

    public ObservableCollection<Galgame> Source { get; private set; } = new();

    public HomeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> dataCollectionService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)_dataCollectionService;
        
        ((GalgameCollectionService)dataCollectionService).GalgameLoadedEvent += async () => Source = await dataCollectionService.GetContentGridDataAsync();

        ItemClickCommand = new RelayCommand<Galgame>(OnItemClick);
    }

    public async void OnNavigatedTo(object parameter)
    {
        Source = await _dataCollectionService.GetContentGridDataAsync();
    }

    public void OnNavigatedFrom()
    {
    }

    private void OnItemClick(Galgame? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, clickedItem.Name);
        }
    }

    [RelayCommand]
    private async void AddGalgame()
    {
        try
        {
            var openPicker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                var result = await _galgameService.TryAddGalgameAsync(folder, true);
                if (result == GalgameCollectionService.AddGalgameResult.Success)
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = "已成功添加游戏";
                    InfoBarSeverity = InfoBarSeverity.Success;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
                else if (result == GalgameCollectionService.AddGalgameResult.AlreadyExists)
                {
                    throw new Exception("库里已经有这个游戏了");
                }
                else //NotFoundInRss
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = "成功添加游戏，但没有从信息源中找到这个游戏的信息";
                    InfoBarSeverity = InfoBarSeverity.Warning;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
            }
        }
        catch (Exception e)
        {
            IsInfoBarOpen = true;
            InfoBarMessage = e.Message;
            InfoBarSeverity = InfoBarSeverity.Error;
            await Task.Delay(3000);
            IsInfoBarOpen = false;
        }
    }
}
