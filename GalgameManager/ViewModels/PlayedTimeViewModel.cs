using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GalgameManager.ViewModels;

public partial class PlayedTimeViewModel : ObservableObject, INavigationAware
{
    public Galgame Game = new();
    private readonly INavigationService _navigationService;
    private readonly IPvnService _pvnService;
    private readonly GalgameCollectionService _galgameCollectionService;
    
    public PlayedTimeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> gameCollectionService,
        IPvnService pvnService)
    {
        _navigationService = navigationService;
        _galgameCollectionService = (gameCollectionService as GalgameCollectionService)!;
        _pvnService = pvnService;
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is Galgame galgame)
        {
            Game = galgame;
            XAxes[0].Labels = galgame.PlayedTime.Keys.ToArray();
            Series[0].Values = galgame.PlayedTime.Values.ToArray();
        }
    }

    public void OnNavigatedFrom()
    {
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
        OnNavigatedTo(Game);
        await _galgameCollectionService.SaveGalgamesAsync(Game);
        _pvnService.Upload(Game, PvnUploadProperties.PlayTime);
    }

    public ISeries[] Series { get; } =
    {
        new ColumnSeries<int>
        {
            Name = "PlayedTime",
            Values = Array.Empty<int>()
        },
    };

    // ReSharper disable once MemberCanBePrivate.Global
    public ICartesianAxis[] XAxes { get; } =
    {
        new Axis()
        {
            Labels = Array.Empty<string>(),
            LabelsRotation = 0,
            SeparatorsPaint = new SolidColorPaint(new SKColor(200, 200, 200)),
            SeparatorsAtCenter = false,
            TicksPaint = new SolidColorPaint(new SKColor(35, 35, 35)),
            TicksAtCenter = true
        }
    };
}