using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;

namespace GalgameManager.Models.BgTasks;

public class SourceMoveTask : BgTaskBase
{
    public Guid GalgameUid //仅用于序列化与反序列化
    {
        get => _game!.Uuid;
        set => _game = _gameService.GetGalgameFromUuid(value);
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
    /// 移出源实例（压缩包/游戏文件夹）是否在物理上删除（仅当物理移出操作执行后才有意义）
    public bool DeleteFilesOnMoveOut { get; init; }

    private Galgame? _game;
    private GalgameSourceBase? _moveInSource;
    private GalgameSourceBase? _moveOutSource;

    private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
    private readonly IGalgameSourceCollectionService _sourceService = App.GetService<IGalgameSourceCollectionService>();
    private readonly IBgTaskService _bgTaskService = App.GetService<IBgTaskService>();

    public SourceMoveTask(Galgame game,GalgameSourceBase? moveInSource, string? moveInPath,
        GalgameSourceBase? moveOutSource, bool deleteFilesOnMoveOut = false)
    {
        _game = game;
        _moveInSource = moveInSource;
        MoveInPath = moveInPath;
        _moveOutSource = moveOutSource;
        DeleteFilesOnMoveOut = deleteFilesOnMoveOut;
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
        GalgameAndPath? sourceEntry = _moveOutSource?.GetEntry(_game!) ?? _game!.PreferredLocalInstallation;
        BgTaskBase bgTask = service.MoveInAsync(_moveInSource, _game!, MoveInPath, sourceEntry);
        await _bgTaskService.AddBgTask(bgTask);
        if (bgTask.Task.IsFaulted)
            throw new PvnException("SourceMoveTask_MovingIn_Fail".GetLocalized());
        // 压缩库的目标路径由PackGameTask自行决定（<库根>/<文件夹名>.zip），登记时须用其真实产物路径；
        // 其余库沿用对话框给出的MoveInPath
        string? registerPath = bgTask is PackGameTask packTask ? packTask.ZipPath : MoveInPath;
        if (registerPath is not null)
        {
            // 压缩库条目不是本地安装，不带LocalInstallationConfig
            LocalInstallationConfig? config = bgTask is PackGameTask
                ? null
                : sourceEntry?.LocalConfig?.Relocated(sourceEntry.Path, registerPath);
            _sourceService.MoveInNoOperate(_moveInSource, _game!, registerPath, config);
        }
    }

    private async Task MoveOutAsync()
    {
        if (_moveOutSource is null) return;
        IGalgameSourceService service = SourceServiceFactory.GetSourceService(_moveOutSource.SourceType);
        BgTaskBase bgTask = service.MoveOutAsync(_moveOutSource, _game!);
        await _bgTaskService.AddBgTask(bgTask);
        if (bgTask.Task.IsFaulted)
            throw new PvnException("SourceMoveTask_MovingOut_Fail".GetLocalized());
        if (_moveOutSource.GetEntry(_game!) is { } entry)
            await _sourceService.MoveOutNoOperate(entry, DeleteFilesOnMoveOut);
    }
}