using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;

namespace GalgameManager.Services;

public class SteamSourceService(IInfoService infoService) : IGalgameSourceService
{
    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null) => 
        throw new PvnException("This source does not support move in operation");

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game) => 
        throw new PvnException("This source does not support move out operation");

    public async Task SaveMetaAsync(Galgame game, GalgameSourceBase? targetSource = null)
    {
        if (!game.CheckExistLocal(GalgameSourceType.Steam)) return;
        foreach (SteamSource source in game.Sources.OfType<SteamSource>().Where(s => s.SaveMetaBackup))
        {
            if (targetSource is not null && source != targetSource) continue; //如果指定了目标源，则只保存到该源
            var basePath = source.MetaPath;
            if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
            if (source.GetPath(game) is not { } gamePath) continue;
            var metaPath = Path.Combine(basePath, $"{new DirectoryInfo(gamePath).Name}");
            LocalFolderSourceService.FolderBaseSaveMeta(game, metaPath);
        }

        await Task.CompletedTask;
    }

    public async Task<Galgame?> LoadMetaAsync(string path)
    {
        await Task.CompletedTask;
        if (!(new DirectoryInfo(path).Parent?.Parent is { } di && di.GetDirectories().Any(d => d.Name == ".PotatoVN")))
        {
            infoService.Log(msg: $"[SteamSourceService] skip load meta for {new DirectoryInfo(path).Name} because .PotatoVN not exist");
            return null;
        }
        var metaFolderPath = Path.Combine(di.FullName, ".PotatoVN", new DirectoryInfo(path).Name);
        return LocalFolderSourceService.FolderBaseLoadMeta(metaFolderPath);
    }

    public Task RemoveMetaAsync(Galgame game)
    {
        return Task.Run(() =>
        {
            foreach (SteamSource source in game.Sources.OfType<SteamSource>())
            {
                try
                {
                    var gamePath = source.GetPath(game)!;
                    var metaPath = Path.Combine(gamePath, ".PotatoVN");
                    if (!Directory.Exists(metaPath)) return;
                    Directory.Delete(metaPath, true);
                    infoService.Log(msg: $"[SteamSourceService] remove meta folder {metaPath}");
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
        DriveInfo info = new(source.Path);
        return Task.FromResult((info.TotalSize, info.TotalSize - info.AvailableFreeSpace));
    }

    public Task AddListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public Task RemoveListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public string GetMoveInDescription(GalgameSourceBase target, string targetPath) => string.Empty; //不支持
    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame) => string.Empty; //不支持

    public async Task<string> GetSourcePathAsync(string gamePath)
    {
        await Task.CompletedTask;
        DirectoryInfo? dir = new(gamePath);
        while (dir is not null)
        {
            if (dir.Name == "steamapps") return dir.FullName;
            dir = dir.Parent;
        }
        throw new PvnException($"{gamePath} does not belong to any Steam library");
    }

    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame) =>
        "This source does not support move operation";

    public Task<string?> SelectPathInSourceAsync(GalgameSourceBase source) =>
        LocalFolderSourceService.FolderBaseSelectPathInSource(source);
}
