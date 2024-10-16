using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;

namespace GalgameManager.ViewModels;

public partial class PlayedTimeViewModel : ObservableObject, INavigationAware
{
    public Galgame Game = new();
    public ObservableCollection<PlayTimeViewModelItem> Items { get; } = new();
    private readonly INavigationService _navigationService;
    private readonly IPvnService _pvnService;
    private readonly GalgameCollectionService _galgameCollectionService;
    private double _totalWidth; // 柱状图绘制区域总宽度
    
    public PlayedTimeViewModel(INavigationService navigationService, IGalgameCollectionService gameCollectionService,
        IPvnService pvnService)
    {
        _navigationService = navigationService;
        _galgameCollectionService = (gameCollectionService as GalgameCollectionService)!;
        _pvnService = pvnService;
    }

    public void OnNavigatedTo(object parameter)
    {
        Debug.Assert(parameter is Galgame);
        if (parameter is not Galgame galgame) return;
        Game = galgame;
        Update();
    }

    public void OnNavigatedFrom()
    {
    }
    
    [RelayCommand]
    private void OnPageSizeChanged(SizeChangedEventArgs e)
    {
        _totalWidth = e.NewSize.Width;
        UpdateWidth();
    }

    /// 更新柱状图
    private void Update()
    {
        Items.Clear();
        foreach (var (date, playTime) in Game.PlayedTime)
            Items.Add(new(date, playTime));
        UpdateWidth();
    }
    
    private void UpdateWidth()
    {
        double maxPlayTime = Game.PlayedTime.Count > 0 ? Game.PlayedTime.Values.Max() : 1;
        foreach (PlayTimeViewModelItem item in Items)
            item.UpdateWidth(_totalWidth, maxPlayTime);
    }

    [RelayCommand]
    private void Back()
    {
        _navigationService.GoBack();
    }

    [RelayCommand]
    private async Task Edit()
    {
        await new EditPlayTimeDialog(Game).ShowAsync();
        await _galgameCollectionService.SaveGalgamesAsync(Game);
        Update();
        _pvnService.Upload(Game, PvnUploadProperties.PlayTime);
    }
}

public partial class PlayTimeViewModelItem : ObservableObject
{
    [ObservableProperty] private double _width;
    [ObservableProperty] private int _playTime;
    [ObservableProperty] private string _date;

    public PlayTimeViewModelItem(string date, int playTime)
    {
        _date = date;
        _playTime = playTime;
    }

    public void UpdateWidth(double totalWidth, double maxPlayTime)
    {
        Width = Math.Max(totalWidth - 200, 0); //预留日期、时间显示区域
        Width = Width * _playTime / maxPlayTime;
    }
}