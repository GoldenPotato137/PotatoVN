/*
 * 游戏源移动任务，当进入托盘模式时由SourceMoveTask恢复
 */

using GalgameManager.Helpers;
using GalgameManager.Models.Sources;

namespace GalgameManager.Models.BgTasks;

public class LocalFolderSourceMoveInTask : BgTaskBase
{
    private readonly Galgame _game;
    private readonly string _targetPath;
    
    public LocalFolderSourceMoveInTask(Galgame game, string targetPath)
    {
        _game = game;
        _targetPath = targetPath;
    }
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        await Task.CompletedTask;
        var originPath = _game.Sources.FirstOrDefault(s => s.SourceType == GalgameSourceType.LocalFolder)
            ?.GetPath(_game);
        if (originPath is null) throw new PvnException("originPath is null");
        if (Utils.IsPathContained(originPath, _targetPath))
            throw new PvnException("TargetPath is contained in originPath");
        
        FolderOperations.Copy(originPath, _targetPath, info =>
        {
            ChangeProgress(0, 1, "LocalFolderSourceMoveTask_MoveIn_Progress".GetLocalized(info.FullName));
        });

        ChangeProgress(1, 1, "LocalFolderSourceMoveTask_MoveIn_Success".GetLocalized(_game.Name, _targetPath));
    }

    public override string Title { get; } = "LocalFolderSourceMoveTask_MoveIn_Title".GetLocalized();
}

public class LocalFolderSourceMoveOutTask : BgTaskBase
{
    private readonly Galgame _game;
    private readonly GalgameSourceBase _target;

    public LocalFolderSourceMoveOutTask(Galgame game, GalgameSourceBase target)
    {
        _game = game;
        _target = target;
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        await Task.CompletedTask;
        var root = _target.GetPath(_game);
        if (root is null) throw new PvnException("root is null"); //不应该发生
        if (!Directory.Exists(root)) throw new PvnException($"{root} not exists");
        
        FolderOperations.Delete(root, info =>
        {
            ChangeProgress(0, 1, "LocalFolderSourceMoveTask_MoveOut_Progress".GetLocalized(info.FullName));
        });
        
        ChangeProgress(1, 1, "LocalFolderSourceMoveTask_MoveOut_Success".GetLocalized(_game.Name, _target.Url));
    }

    public override string Title { get; } = "LocalFolderSourceMoveTask_MoveOut_Title".GetLocalized();
}