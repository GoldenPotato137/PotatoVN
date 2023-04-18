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

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class GalgameViewModel : ObservableRecipient, INavigationAware
{
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly INavigationService _navigationService;
    private readonly JumpListService _jumpListService;
    private Galgame? _item;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Visibility _isTagVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isDescriptionVisible = Visibility.Collapsed;

    public Galgame? Item
    {
        get => _item;
        private set => SetProperty(ref _item, value);
    }

    public GalgameViewModel(IDataCollectionService<Galgame> dataCollectionService, INavigationService navigationService, IJumpListService jumpListService)
    {
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _navigationService = navigationService;
        _galgameService.PhrasedEvent += () => IsPhrasing = false;
        _jumpListService = (JumpListService)jumpListService;
        Item = new Galgame();
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is not string path) return;
        {
            var data = await _dataCollectionService.GetContentGridDataAsync();
            try
            {
                Item = data.First(i => i.Path == path);
                Item.CheckSavePosition();
                UpdateVisibility();
            }
            catch (Exception) //找不到这个游戏，回到主界面
            {
                _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
            }
        }
    }

    public void OnNavigatedFrom()
    {
    }
    
    private void UpdateVisibility()
    {
        IsTagVisible = Item?.Tags.Value?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        IsDescriptionVisible = Item?.Description! != string.Empty ? Visibility.Visible : Visibility.Collapsed;
    }

    [RelayCommand]
    private async void Play()
    {
        if (Item == null) return;
        if (Item.ExePath == null)
            await _galgameService.GetGalgameExeAsync(Item);
        if (Item.ExePath == null) return;

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
        await _jumpListService.AddToJumpListAsync(Item);
    }

    [RelayCommand]
    private async void GetInfoFromRss()
    {
        if (Item == null) return;
        IsPhrasing = true;
        await _galgameService.PhraseGalInfoAsync(Item);
        UpdateVisibility();
    }

    [RelayCommand]
    private void Setting()
    {
        if (Item == null) return;
        _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, Item);
    }

    [RelayCommand]
    private async void ChangeSavePosition()
    {
        if(Item == null) return;
        await _galgameService.ChangeGalgameSavePosition(Item);
        await Task.Delay(1000); //等待1000ms建立软连接后再刷新
        Item.CheckSavePosition();
    }
}