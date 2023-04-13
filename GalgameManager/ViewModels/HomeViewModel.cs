using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class HomeViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _isPhrasing;

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
        _galgameService.PhrasedEvent += () => IsPhrasing = false;
        // IsPhrasing = _galgameService.IsPhrasing;

        ItemClickCommand = new RelayCommand<Galgame>(OnItemClick);
    }

    public async void OnNavigatedTo(object parameter)
    {
        // var newItems = await _dataCollectionService.GetContentGridDataAsync();
        // Source.Clear();
        // foreach (var item in newItems)
        // {
        //     Source.Add(item);
        // }
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
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, clickedItem.Path);
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
                IsPhrasing = true;
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
            IsPhrasing = false;
            IsInfoBarOpen = true;
            InfoBarMessage = e.Message;
            InfoBarSeverity = InfoBarSeverity.Error;
            await Task.Delay(3000);
            IsInfoBarOpen = false;
        }
    }

    [RelayCommand]
    private async void GalFlyOutDelete(Galgame? galgame)
    {
        if(galgame == null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "取消托管",
            Content = "确定要取消托管这个游戏吗",
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消"
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(galgame);
        };
        
        await dialog.ShowAsync();
    }
    
    [RelayCommand]
    private void GalFlyOutEdit(Galgame? galgame)
    {
        if(galgame == null) return;
        _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame);
    }
    
    [RelayCommand]
    private async void GalFlyOutDeleteFromDisk(Galgame? galgame)
    {
        if(galgame == null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "删除游戏",
            Content = "确定要从硬盘上移除这个游戏吗，这个操作不可逆哦",
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消"
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(galgame, true);
        };
        
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async void GalFlyOutGetInfoFromRss(Galgame? galgame)
    {
        if(galgame == null) return;
        IsPhrasing = true;
        await _galgameService.PhraseGalInfoAsync(galgame);
        IsPhrasing = false;
    }
}
