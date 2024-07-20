using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;

namespace GalgameManager.Models.BgTasks;

public class SourceMoveTask : BgTaskBase
{
    public GalgameUid GalgameUid //仅用于序列化与反序列化
    {
        get => _game!.Uid;
        set => _game = _gameService.GetGalgameFromUid(value);
    }
    public string? MoveInPath { get; init; }
    public string? MoveInSourceUrl //仅用于序列化与反序列化
    {
        get => _moveInSource?.Url;
        set => _moveInSource = _sourceService.GetGalgameSourceFromUrl(value ?? string.Empty);
    }
    public string? MoveOutSourceUrl //仅用于序列化与反序列化
    {
        get => _moveOutSource?.Url;
        set => _moveOutSource = _sourceService.GetGalgameSourceFromUrl(value ?? string.Empty);
    }
    
    private Galgame? _game;
    private GalgameSourceBase? _moveInSource;
    private GalgameSourceBase? _moveOutSource;

    private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
    private readonly IGalgameSourceCollectionService _sourceService = App.GetService<IGalgameSourceCollectionService>();
    private readonly IBgTaskService _bgTaskService = App.GetService<IBgTaskService>();

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
        if (_game is null) throw new InvalidOperationException($"Can't find game with uid {GalgameUid}");
        
        ChangeProgress(0, 2, "SourceMoveTask_MovingIn".GetLocalized());
        await MoveInAsync();
        
        ChangeProgress(1, 2, "SourceMoveTask_MovingOut".GetLocalized());
        await MoveOutAsync();
        
        List<string> msg = new();
        if (_moveInSource is not null)
            msg.Add("SourceMoveTask_Success_MoveIn".GetLocalized(_game.Name.Value ?? string.Empty, _moveInSource.Url));
        if (_moveOutSource is not null)
            msg.Add("SourceMoveTask_Success_MoveOut".GetLocalized(_game.Name.Value ?? string.Empty, _moveOutSource.Url));
        ChangeProgress(2, 2, string.Join('\n', msg));
    }

    public override string Title { get; } = "SourceMoveTask_Title".GetLocalized();

    private async Task MoveInAsync()
    {
        if (_moveInSource is null) return;
        IGalgameSourceService service = SourceServiceFactory.GetSourceService(_moveInSource.SourceType);
        BgTaskBase bgTask = service.MoveInAsync(_moveInSource, _game!, MoveInPath);
        await _bgTaskService.AddBgTask(bgTask);
        if (bgTask.Task.IsFaulted)
            throw new PvnException("SourceMoveTask_MovingIn_Fail".GetLocalized());
    }

    private async Task MoveOutAsync()
    {
        if (_moveOutSource is null) return;
        IGalgameSourceService service = SourceServiceFactory.GetSourceService(_moveOutSource.SourceType);
        BgTaskBase bgTask = service.MoveOutAsync(_moveOutSource, _game!);
        await _bgTaskService.AddBgTask(bgTask);
        if (bgTask.Task.IsFaulted)
            throw new PvnException("SourceMoveTask_MovingOut_Fail".GetLocalized());
    }
}