using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class GalgameViewModel : ObservableRecipient, INavigationAware
{
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly JumpListService _jumpListService;
    private Galgame? _item;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Visibility _isTagVisible = Visibility.Collapsed;
    [ObservableProperty] private Visibility _isDescriptionVisible = Visibility.Collapsed;

    #region UI_STRINGS

    public readonly string UiPlay ="GalgamePage_Play".GetLocalized();
    public readonly string UiEdit = "GalgamePage_Edit".GetLocalized();
    public readonly string UiChangeSavePosition = "GalgamePage_ChangeSavePosition".GetLocalized();
    public readonly string UiDevelopers = "GalgamePage_Developers".GetLocalized();
    public readonly string UiExpectedPlayTime = "GalgamePage_ExpectedPlayTime".GetLocalized();
    public readonly string UiSavePosition = "GalgamePage_SavePosition".GetLocalized();
    public readonly string UiLastPlayTime = "GalgamePage_LastPlayTime".GetLocalized();
    public readonly string UiDescription = "GalgamePage_Description".GetLocalized();
    public readonly string UiPlayFlyOutTitle = "GalgamePage_UiPlayFlyOutTitle".GetLocalized();
    public readonly string UiYes = "Yes".GetLocalized();
    
    #endregion
    
    public Galgame? Item
    {
        get => _item;
        private set => SetProperty(ref _item, value);
    }

    public GalgameViewModel(IDataCollectionService<Galgame> dataCollectionService, INavigationService navigationService, IJumpListService jumpListService, ILocalSettingsService localSettingsService)
    {
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _navigationService = navigationService;
        _galgameService.PhrasedEvent += () => IsPhrasing = false;
        _jumpListService = (JumpListService)jumpListService;
        _localSettingsService = localSettingsService;
        Item = new Galgame();
    }

    public async void OnNavigatedTo(object parameter)
    {
        var path = parameter as string ?? null;
        var startGame = false;
        Tuple<string, bool>? para = parameter as Tuple<string, bool> ?? null;
        if (para != null)
        {
            path = para.Item1;
            startGame = para.Item2;
        }

        ObservableCollection<Galgame>? data = await _dataCollectionService.GetContentGridDataAsync();
        try
        {
            Item = data.First(i => i.Path == path);
            Item.CheckSavePosition();
            UpdateVisibility();
            if (startGame && await _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart))
                await Play();
        }
        catch (Exception) //找不到这个游戏，回到主界面
        {
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
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
    private async Task Play()
    {
        if (Item == null) return;
        if (Item.ExePath == null)
            await _galgameService.GetGalgameExeAsync(Item);
        if (Item.ExePath == null) return;

        Item.LastPlay = DateTime.Now.ToShortDateString();
        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Item.ExePath,
                WorkingDirectory = Item.Path,
                UseShellExecute = false
            }
        };
        process.Start();
        _galgameService.Sort();
        await Task.Delay(1000); //等待1000ms，让游戏进程启动后再最小化
        ((OverlappedPresenter)App.MainWindow.AppWindow.Presenter).Minimize(); //最小化窗口
        await _jumpListService.AddToJumpListAsync(Item);

        await process.WaitForExitAsync();
        ((OverlappedPresenter)App.MainWindow.AppWindow.Presenter).Restore(); //恢复窗口
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
    
    [RelayCommand]
    private void ResetExePath(object obj)
    {
        Item!.ExePath = null;
    }
}