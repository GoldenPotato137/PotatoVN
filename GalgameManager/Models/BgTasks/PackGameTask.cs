using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace GalgameManager.Models.BgTasks;

/// 把游戏文件夹压缩成zip并移入压缩库（物理操作）
public class PackGameTask : BgTaskBase
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
            TotalBytes = TotalBytes > 0 ? TotalBytes : GetDirectorySize(GamePath);
            await PackAsync();

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
            if (_targetSource.SaveMetaBackup)
                await Task.Run(() => LocalFolderSourceService.FolderBaseSaveMeta(_game,
                    ZipSourceService.GetMetaPath(_targetSource.Path, ZipPath)));
            ChangeProgress(1, 1, "PackGameTask_Done".GetLocalized(ZipPath));
        }
        catch (Exception e) when (e is not PvnException)
        {
            if (File.Exists(ZipPath)) File.Delete(ZipPath); // 删除半成品，避免下次因为文件存在而失败
            throw new PvnException("PackGameTask_Failed".GetLocalized(e.Message));
        }
    }

    private async Task PackAsync()
    {
        using FileStream stream = File.Create(ZipPath);
        using var archive = ZipArchive.CreateArchive();
        archive.AddAllFromDirectory(GamePath);
        await Task.Run(() => archive.SaveTo(stream, CompressionType.Deflate));
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
