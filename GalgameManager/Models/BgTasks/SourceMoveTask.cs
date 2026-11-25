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
    /// 移出原库时是否物理删除原库中的文件（压缩包/游戏文件夹）；为false时只解除游戏与原库的关联
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

    /// <summary>
    /// 决定移入其他库时以哪个条目的文件作为来源：优先使用移出库中的条目（若该条目有实际文件），
    /// 其次是首选本地安装实例，最后是任意压缩库条目
    /// </summary>
    public static GalgameAndPath? ResolveSourceEntry(Galgame game, GalgameSourceBase? moveOutSource)
    {
        if (moveOutSource?.GetEntry(game) is { } entry && HasFiles(entry)) return entry;
        return game.PreferredLocalInstallation
               ?? game.SourceEntries.FirstOrDefault(e => e.Source is GalgameZipSource);
    }

    /// 条目是否对应磁盘上的游戏文件（本地安装文件夹或压缩包），即能否作为移动的来源
    public static bool HasFiles(GalgameAndPath entry) => entry.IsLocalInstallation || entry.Source is GalgameZipSource;

    private async Task MoveInAsync()
    {
        if (_moveInSource is null) return;
        IGalgameSourceService service = SourceServiceFactory.GetSourceService(_moveInSource.SourceType);
        GalgameAndPath? sourceEntry = ResolveSourceEntry(_game!, _moveOutSource);
        BgTaskBase bgTask = service.MoveInAsync(_moveInSource, _game!, MoveInPath, sourceEntry);
        await _bgTaskService.AddBgTask(bgTask);
        if (bgTask.Task.IsFaulted)
            throw new PvnException("SourceMoveTask_MovingIn_Fail".GetLocalized());
        // 目标路径通常由service决定（如压缩为<库根>/<文件夹名>.zip、解压为<库根>/<压缩包名>），登记时以真实产物路径为准
        string? registerPath = bgTask is IMoveInTask moveInTask ? moveInTask.ResultPath : MoveInPath;
        if (registerPath is null) return;
        // 安装配置中的路径随条目根路径迁移；压缩库条目保存的是以压缩包为根的配置，解压时再迁移回本地路径
        LocalInstallationConfig? config = sourceEntry?.LocalConfig?.Relocated(sourceEntry.Path, registerPath);
        _sourceService.MoveInNoOperate(_moveInSource, _game!, registerPath, config);
    }

    private async Task MoveOutAsync()
    {
        if (_moveOutSource is null) return;
        if (_moveOutSource.GetEntry(_game!) is not { } entry) return;
        if (DeleteFilesOnMoveOut)
        {
            IGalgameSourceService service = SourceServiceFactory.GetSourceService(_moveOutSource.SourceType);
            BgTaskBase bgTask = service.MoveOutAsync(_moveOutSource, _game!);
            await _bgTaskService.AddBgTask(bgTask);
            if (bgTask.Task.IsFaulted)
                throw new PvnException("SourceMoveTask_MovingOut_Fail".GetLocalized());
        }
        // 物理删除（若有）已由service任务完成，这里只解除关联
        await _sourceService.MoveOutNoOperate(entry);
    }
}