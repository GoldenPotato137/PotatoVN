using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;

namespace GalgameManager.Services;

public class ZipSourceService(IInfoService infoService, IGalgameSourceCollectionService sourceCollectionService)
    : IGalgameSourceService
{
    /// 库根目录下存放元数据备份的文件夹名（与本地库游戏文件夹内的.PotatoVN同格式）
    public const string MetaFolderName = ".PotatoVN";

    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null,
        GalgameAndPath? sourceEntry = null)
    {
        if (target is not GalgameZipSource zipSource) throw new ArgumentException("target is not GalgameZipSource");
        sourceEntry ??= SourceMoveTask.ResolveSourceEntry(game, null);
        // 来源为压缩包（其他压缩库）时原样复制所有分卷，不解压重压
        if (sourceEntry?.Source is GalgameZipSource)
            return new CopyPackTask(game, sourceEntry, zipSource);
        if (sourceEntry is not { IsLocalInstallation: true } || !Directory.Exists(sourceEntry.Path))
            throw new PvnException("ZipSourceService_NoLocalInstallation".GetLocalized());
        // 移入即压缩到 <库根>/<当前文件夹名>.zip，不支持指定目标路径
        return new PackGameTask(game, sourceEntry, zipSource, GetPackPath(zipSource, sourceEntry));
    }

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game) => new ZipSourceMoveOutTask(game, target);

    public async Task SaveMetaAsync(Galgame game, GalgameSourceBase? targetSource = null)
    {
        await Task.CompletedTask;
        foreach (GalgameZipSource source in game.Sources.OfType<GalgameZipSource>().Where(s => s.SaveMetaBackup))
        {
            if (targetSource is not null && source != targetSource) continue; //如果指定了目标源，则只保存到该源
            if (source.GetPath(game) is not { } packPath) continue;
            var metaPath = GetMetaPath(source.Path, packPath);
            LocalFolderSourceService.FolderBaseSaveMeta(game, metaPath, source.GetEntry(game)?.LocalConfig);
        }
    }

    public async Task<Galgame?> LoadMetaAsync(string path)
    {
        await Task.CompletedTask;
        if (!File.Exists(path)) return null;
        // 压缩包可能位于已注册压缩库的子文件夹中，优先匹配 deepest 的库；无匹配时退化为同级目录
        GalgameSourceBase? source = sourceCollectionService.GetGalgameSources()
            .Where(s => s.SourceType == GalgameSourceType.LocalZip && s.IsInSource(path))
            .MaxBy(s => s.Path.Length);
        if (source is null && GetSourcePathFromPath(path) is null) return null;
        var rootPath = source?.Path ?? GetSourcePathFromPath(path)!;
        return LocalFolderSourceService.FolderBaseLoadMeta(GetMetaPath(rootPath, path), path);
    }

    public Task RemoveMetaAsync(Galgame game)
    {
        return Task.Run(() =>
        {
            foreach (GalgameZipSource source in game.Sources.OfType<GalgameZipSource>())
            {
                try
                {
                    if (source.GetPath(game) is not { } packPath) continue;
                    var metaPath = GetMetaPath(source.Path, packPath);
                    if (!Directory.Exists(metaPath)) continue;
                    Directory.Delete(metaPath, true);
                    infoService.Log(msg: $"[ZipSourceService] remove meta folder {metaPath}");
                }
                catch (Exception e)
                {
                    infoService.DeveloperEvent(msg: $"failed to remove meta folder with exception: {e}");
                }
            }
        });
    }

    public Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source)
    {
        try
        {
            DriveInfo? info = GetDriveInfo(source.Path);
            if (info is null) return Task.FromResult(((long, long))(-1, -1));
            return Task.FromResult((info.TotalSize, info.TotalSize - info.AvailableFreeSpace));
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(msg: $"failed to get drive info with exception: {e}");
            return Task.FromResult(((long, long))(-1, -1));
        }
    }

    public Task AddListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public Task RemoveListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public string GetMoveInDescription(GalgameSourceBase target, Galgame galgame, GalgameAndPath? sourceEntry)
    {
        if (sourceEntry is null) return string.Empty;
        return sourceEntry.Source is GalgameZipSource
            ? "ZipSourceService_CopyInDescription".GetLocalized(sourceEntry.Path, GetPackPath(target, sourceEntry))
            : "ZipSourceService_MoveInDescription".GetLocalized(GetPackPath(target, sourceEntry));
    }

    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame, bool deleteFiles)
    {
        var path = target.GetPath(galgame) ?? string.Empty;
        return deleteFiles
            ? "ZipSourceService_MoveOutDescription".GetLocalized(path)
            : "ZipSourceService_UnlinkDescription".GetLocalized(path);
    }

    public Task<string> GetSourcePathAsync(string gamePath)
    {
        // 优先匹配已注册且包含该路径的压缩库（取路径最长者，即最深的库）
        GalgameSourceBase? source = sourceCollectionService.GetGalgameSources()
            .Where(s => s.SourceType == GalgameSourceType.LocalZip && s.IsInSource(gamePath))
            .MaxBy(s => s.Path.Length);
        if (source is not null) return Task.FromResult(source.Path);
        // 无匹配时退化：压缩文件所在目录
        return Task.FromResult(GetSourcePathFromPath(gamePath)
            ?? throw new PvnException($"{gamePath} does not belong to any zip library"));
    }

    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame)
    {
        if (moveIn is not GalgameZipSource target) return null;
        GalgameAndPath? sourceEntry = SourceMoveTask.ResolveSourceEntry(galgame, moveOut);
        if (sourceEntry?.Source is GalgameZipSource)
        {
            if (!File.Exists(sourceEntry.Path))
                return "GalgameFolder_UnpackGame_PackNotExist".GetLocalized(sourceEntry.Path);
            // 任意一个分卷在目标库中已存在都不能复制
            var existing = GetCopyVolumes(target, sourceEntry.Path).Select(v => v.To).FirstOrDefault(Path.Exists);
            return existing is null ? null : "ZipSourceService_TargetExists".GetLocalized(existing);
        }
        if (sourceEntry is not { IsLocalInstallation: true } || !Directory.Exists(sourceEntry.Path))
            return "ZipSourceService_NoLocalInstallation".GetLocalized();
        var packPath = GetPackPath(target, sourceEntry);
        return Path.Exists(packPath) ? "ZipSourceService_TargetExists".GetLocalized(packPath) : null;
    }

    public Task<string?> SelectPathInSourceAsync(GalgameSourceBase source) =>
        LocalFolderSourceService.FolderBaseSelectPathInSource(source);

    /// 获取某个压缩包对应的元数据备份文件夹路径：&lt;库根&gt;/.PotatoVN/&lt;包名&gt;
    public static string GetMetaPath(string sourceRoot, string packPath) =>
        Path.Combine(sourceRoot, MetaFolderName, GalgameZipSource.GetPackName(packPath));

    /// 移入压缩库后的压缩包路径：本地安装压缩为 &lt;库根&gt;/&lt;来源文件夹名&gt;.zip；
    /// 来源为压缩包时原名复制到 &lt;库根&gt;/&lt;压缩包（第一卷）文件名&gt;
    public static string GetPackPath(GalgameSourceBase target, GalgameAndPath sourceEntry) =>
        sourceEntry.Source is GalgameZipSource
            ? Path.Combine(target.Path, Path.GetFileName(sourceEntry.Path))
            : Path.Combine(target.Path, $"{new DirectoryInfo(sourceEntry.Path).Name.RemoveInvalidChars()}.zip");

    /// 把压缩包复制到另一个压缩库时，每个分卷的来源与目标路径（目标为 &lt;库根&gt;/&lt;分卷文件名&gt;）
    public static List<(string From, string To)> GetCopyVolumes(GalgameSourceBase target, string packPath) =>
        GalgameZipSource.GetVolumePaths(packPath)
            .Select(volume => (volume, Path.Combine(target.Path, Path.GetFileName(volume))))
            .ToList();

    /// 删除压缩包的所有分卷及其元数据备份文件夹
    /// <param name="sourceRoot">压缩库根目录</param>
    /// <param name="packPath">压缩包（第一卷）路径</param>
    /// <param name="onDelete">删除每个分卷前的回调，参数为分卷路径</param>
    public static void DeletePackFiles(string sourceRoot, string packPath, Action<string>? onDelete = null)
    {
        foreach (var volume in GalgameZipSource.GetVolumePaths(packPath))
        {
            onDelete?.Invoke(volume);
            File.Delete(volume);
        }
        var metaPath = GetMetaPath(sourceRoot, packPath);
        if (Directory.Exists(metaPath)) Directory.Delete(metaPath, true);
    }

    private static string? GetSourcePathFromPath(string packPath) =>
        new FileInfo(packPath).Directory?.FullName;

    private static DriveInfo? GetDriveInfo(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is null ? null : new DriveInfo(root);
    }
}
