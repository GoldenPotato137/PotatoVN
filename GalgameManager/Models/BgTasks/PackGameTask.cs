using System.Text;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;

namespace GalgameManager.Models.BgTasks;

/// 把游戏文件夹压缩成zip并移入压缩库（物理操作）
public class PackGameTask : BgTaskBase, IMoveInTask
{
    public string GamePath = string.Empty;
    public string ZipPath = string.Empty;
    /// 目标压缩库Url，仅用于序列化与反序列化
    public string? TargetSourceUrl;
    /// 压缩包预期大小（等于游戏文件夹总大小），用于UI显示；若实际远超预期则说明压缩失败或文件夹发生了变化
    public long TotalBytes;
    public string GameUuid //仅用于序列化与反序列化
    {
        get => _game?.Uuid.ToString() ?? string.Empty;
        set => _game = App.GetService<IGalgameCollectionService>().GetGalgameFromUuid(
            Guid.TryParse(value, out Guid uid) ? uid : Guid.Empty);
    }

    [JsonIgnore] public string ResultPath => ZipPath;

    private Galgame? _game;
    private GalgameAndPath? _sourceEntry;
    private GalgameZipSource? _targetSource;

    public PackGameTask() { }

    public PackGameTask(Galgame game, GalgameAndPath sourceEntry, GalgameZipSource targetSource,
        string zipPath)
    {
        _game = game;
        _sourceEntry = sourceEntry;
        _targetSource = targetSource;
        GamePath = sourceEntry.Path;
        ZipPath = zipPath;
        TargetSourceUrl = targetSource.Url;
    }

    protected override Task RecoverFromJsonInternal()
    {
        IGalgameSourceCollectionService sourceService = App.GetService<IGalgameSourceCollectionService>();
        _targetSource = sourceService.GetGalgameSourceFromUrl(TargetSourceUrl ?? string.Empty) as GalgameZipSource;
        // 上次中断可能留下半成品压缩包
        if (File.Exists(ZipPath))
        {
            try { File.Delete(ZipPath); }
            catch { /* 忽略，RunInternal里会再检查 */ }
        }
        return Task.CompletedTask;
    }

    /// 注意：必须为async方法，前置检查的异常才会被捕获进返回的Task（BgTaskBase.Task），
    /// 否则同步抛出的异常会导致Task属性停留在默认的CompletedTask，调用方无法感知失败
    protected override async Task RunInternal()
    {
        if (_game is null || _targetSource is null)
            throw new PvnException("PackGameTask_Recovered_RequiredInfoLost".GetLocalized());
        if (!Path.Exists(GamePath))
            throw new PvnException("PackGameTask_GamePathNotExist".GetLocalized(GamePath));
        if (Path.Exists(ZipPath))
        {
            if (StartFromBg) File.Delete(ZipPath);
            else throw new PvnException("PackGameTask_ZipExist".GetLocalized(ZipPath));
        }

        try
        {
            ChangeProgress(0, 1, "PackGameTask_Compressing".GetLocalized(GamePath));
            // BgTaskService在调用方线程上启动任务，耗时操作必须放到后台线程，否则会卡住UI
            await Task.Run(() =>
            {
                TotalBytes = TotalBytes > 0 ? TotalBytes : GetDirectorySize(GamePath);
                Pack();
            });

            // 体积异常检查仅作提示（游戏通常远大于压缩后产物；微小文件夹会误报），绝不能因此让任务失败
            try
            {
                if (new FileInfo(ZipPath).Length > TotalBytes * 2)
                    App.GetService<IInfoService>().Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                        msg: $"[PackGameTask] {ZipPath} is larger than 2x of source folder size, is it really a game?");
            }
            catch
            {
                // best-effort logging
            }

            ChangeProgress(0, 1, "PackGameTask_Registering".GetLocalized());
            // 压缩完成后立刻写入压缩库sidecar元数据备份，保证此刻起可离线恢复
            // 安装配置中的路径改为以压缩包路径为根，解压时再迁移到解压位置（见SourceMoveTask）
            if (_targetSource.SaveMetaBackup)
                await Task.Run(() => LocalFolderSourceService.FolderBaseSaveMeta(_game,
                    ZipSourceService.GetMetaPath(_targetSource.Path, ZipPath),
                    _sourceEntry?.LocalConfig?.Relocated(GamePath, ZipPath)));
            ChangeProgress(1, 1, "PackGameTask_Done".GetLocalized(ZipPath));
        }
        catch (Exception e) when (e is not PvnException)
        {
            if (File.Exists(ZipPath)) File.Delete(ZipPath); // 删除半成品，避免下次因为文件存在而失败
            throw new PvnException("PackGameTask_Failed".GetLocalized(e.Message));
        }
    }

    private void Pack()
    {
        using FileStream stream = File.Create(ZipPath);
        using var archive = ZipArchive.CreateArchive();
        archive.AddAllFromDirectory(GamePath);
        archive.SaveTo(stream, new ZipWriterOptions(CompressionType.Deflate)
        {
            // SharpCompress只在编码与Encoding.UTF8相等时才写入UTF-8标志位（bit 11），默认的Encoding.Default（无BOM的UTF-8）与之不相等，
            // 导致文件名按UTF-8写入却没有标志位，资源管理器/7-Zip等会按系统代码页解读，非ASCII文件名变成乱码
            ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 },
            Progress = new PackProgress(this),
        });
    }

    /// SharpCompress按条目报告进度（每个条目的BytesTransferred从0开始），这里累加成整体进度。
    /// 不能用System.Progress：它会把回调投递到线程池异步执行，乱序后无法正确累加
    private sealed class PackProgress(PackGameTask task) : IProgress<ProgressReport>
    {
        private string? _entry;
        private long _entryBytes; // 当前条目已读取的字节数
        private long _finishedBytes; // 已完成条目的字节数之和
        private long _lastPercent = -1;

        public void Report(ProgressReport value)
        {
            if (value.EntryPath != _entry)
            {
                _finishedBytes += _entryBytes;
                _entry = value.EntryPath;
            }
            _entryBytes = value.BytesTransferred;
            if (task.TotalBytes <= 0) return;
            // Current>=Total会被视为任务完成，压缩完成前最多显示99
            var percent = Math.Min((_finishedBytes + _entryBytes) * 100 / task.TotalBytes, 99);
            if (percent == _lastPercent) return; // 每读取一块数据都会回调一次，只在百分比变化时更新UI
            _lastPercent = percent;
            task.ChangeProgress(percent, 100, "PackGameTask_Compressing".GetLocalized(task.GamePath));
        }
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    public override bool OnSearch(string key) => Utils.ArePathsEqual(key, ZipPath) || Utils.ArePathsEqual(key, GamePath);

    public override string Title => "PackGameTask_Title".GetLocalized();
}
