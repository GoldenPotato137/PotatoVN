using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;

namespace GalgameManager.Models.BgTasks;

/// <summary>
/// 把压缩包（含全部分卷）原样复制到另一个压缩库（压缩库之间移动游戏），不解压重压
/// </summary>
public class CopyPackTask : BgTaskBase, IMoveInTask
{
    private readonly Galgame _game;
    private readonly GalgameAndPath _sourceEntry; // 来源压缩库中的条目（路径为第一卷）
    private readonly GalgameZipSource _target;

    public string ResultPath { get; }

    public CopyPackTask(Galgame game, GalgameAndPath sourceEntry, GalgameZipSource target)
    {
        _game = game;
        _sourceEntry = sourceEntry;
        _target = target;
        ResultPath = ZipSourceService.GetPackPath(target, sourceEntry);
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        var packPath = _sourceEntry.Path;
        if (!File.Exists(packPath))
            throw new PvnException("GalgameFolder_UnpackGame_PackNotExist".GetLocalized(packPath));
        List<(string From, string To)> volumes = ZipSourceService.GetCopyVolumes(_target, packPath);
        if (volumes.Select(v => v.To).FirstOrDefault(Path.Exists) is { } existing)
            throw new PvnException("ZipSourceService_TargetExists".GetLocalized(existing));

        List<string> copied = [];
        try
        {
            // BgTaskService在调用方线程上启动任务，复制必须放到后台线程，否则会卡住UI
            await Task.Run(() =>
            {
                for (var i = 0; i < volumes.Count; i++)
                {
                    var (from, to) = volumes[i];
                    ChangeProgress(i, volumes.Count, "CopyPackTask_Progress".GetLocalized(from));
                    if (File.Exists(to)) throw new PvnException("ZipSourceService_TargetExists".GetLocalized(to));
                    copied.Add(to);
                    File.Copy(from, to);
                }
            });
            // 与PackGameTask一致：复制完成后立刻写入目标库的sidecar元数据备份，安装配置改为以新压缩包路径为根
            if (_target.SaveMetaBackup)
                await Task.Run(() => LocalFolderSourceService.FolderBaseSaveMeta(_game,
                    ZipSourceService.GetMetaPath(_target.Path, ResultPath),
                    _sourceEntry.LocalConfig?.Relocated(packPath, ResultPath)));
        }
        catch
        {
            // 目标分卷在复制前都不存在，失败时删除已复制（或复制了一半）的分卷
            foreach (var file in copied)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ignore
                }
            }
            throw;
        }

        ChangeProgress(1, 1, "CopyPackTask_Done".GetLocalized(ResultPath));
    }

    public override bool OnSearch(string key) =>
        Utils.ArePathsEqual(key, _sourceEntry.Path) || Utils.ArePathsEqual(key, ResultPath);

    public override string Title { get; } = "CopyPackTask_Title".GetLocalized();
}
