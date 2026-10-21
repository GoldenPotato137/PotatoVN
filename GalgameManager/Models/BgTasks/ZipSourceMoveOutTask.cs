using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;

namespace GalgameManager.Models.BgTasks;

/// <summary>
/// 删除游戏在压缩库中的压缩包（含全部分卷）及其元数据备份，当进入托盘模式时由SourceMoveTask恢复
/// </summary>
public class ZipSourceMoveOutTask : BgTaskBase
{
    private readonly Galgame _game;
    private readonly GalgameSourceBase _target;

    public ZipSourceMoveOutTask(Galgame game, GalgameSourceBase target)
    {
        _game = game;
        _target = target;
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        var packPath = _target.GetPath(_game);
        if (packPath is null) throw new PvnException("pack path is null"); //不应该发生
        if (!File.Exists(packPath)) throw new PvnException($"{packPath} not exists");

        await Task.Run(() => ZipSourceService.DeletePackFiles(_target.Path, packPath, file =>
        {
            ChangeProgress(0, 1, "ZipSourceMoveOutTask_Progress".GetLocalized(file));
        }));

        ChangeProgress(1, 1, "ZipSourceMoveOutTask_Success".GetLocalized(packPath));
    }

    public override string Title { get; } = "ZipSourceMoveOutTask_Title".GetLocalized();
}
