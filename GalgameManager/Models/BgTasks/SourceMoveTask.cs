using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class SourceMoveTask : BgTaskBase
{
    public GalgameUid GalgameUid
    {
        get => _game!.Uid;
        set => _game = _gameService.GetGalgameFromUid(value);
    }
    public string? MoveInPath { get; init; }
    public string? MoveInSourceUrl
    {
        get => _moveInSource?.Url;
        set => _moveInSource = _sourceService.GetGalgameSourceFromUrl(value ?? string.Empty);
    }
    public string? MoveOutSourceUrl
    {
        get => _moveOutSource?.Url;
        set => _moveOutSource = _sourceService.GetGalgameSourceFromUrl(value ?? string.Empty);
    }
    
    private Galgame? _game;
    private GalgameSourceBase? _moveInSource;
    private GalgameSourceBase? _moveOutSource;

    private readonly GalgameCollectionService _gameService =
        (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
    private readonly IGalgameSourceCollectionService _sourceService = App.GetService<IGalgameSourceCollectionService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    
    public SourceMoveTask(Galgame game,GalgameSourceBase? moveInSource, string? moveInPath, 
        GalgameSourceBase? moveOutSource)
    {
        _game = game;
        _moveInSource = moveInSource;
        MoveInPath = moveInPath;
        _moveOutSource = moveOutSource;
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask; // 不需要

    protected async override Task RunInternal()
    {
        await Task.CompletedTask;
        ChangeProgress(0, 2, string.Empty);
        while (CurrentProgress.Current != 2)
        {
            switch (CurrentProgress.Current)
            {
                case 0: // MoveIn
                    ChangeProgress(0, 2, "SourceMoveTask_MovingIn");
                    if (_moveInSource is not null && MoveInPath is null)
                    {
                        _infoService.DeveloperEvent(InfoBarSeverity.Error,
                            "move in path is null but move in source is not null");
                        return;
                    }

                    ChangeProgress(1, 2, string.Empty);
                    break;
                case 1: // MoveOut
                    break;
            }
        }
    }

    public override string Title { get; } = "SourceMoveTask_Title".GetLocalized();
}