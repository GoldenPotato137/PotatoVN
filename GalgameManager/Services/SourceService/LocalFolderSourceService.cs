using System.Web;
using Windows.Storage;
using Windows.Storage.Pickers;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class LocalFolderSourceService : IGalgameSourceService
{
    private readonly Dictionary<GalgameFolderSource, FileSystemWatcher> _watchers = new();
    private readonly IInfoService _infoService;
    private readonly IFileService _fileService;

    public LocalFolderSourceService(IInfoService infoService, IFileService fileService)
    {
        _infoService = infoService;
        _fileService = fileService;
        App.OnAppClosing += () =>
        {
            foreach (FileSystemWatcher watcher in _watchers.Values)
            {
                try
                {
                    watcher.Dispose();
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        };
    }

    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null)
    {
        if (targetPath is null) throw new PvnException("targetPath is null");
        if (target is not GalgameFolderSource) throw new ArgumentException("target is not GalgameFolderSource");
        return new LocalFolderSourceMoveInTask(game, targetPath);
    }

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game)
    {
        return new LocalFolderSourceMoveOutTask(game, target);
    }

    public async Task SaveMetaAsync(Galgame game, GalgameSourceBase? targetSource = null)
    {
        if (!game.CheckExistLocal()) return;
        foreach (GalgameFolderSource source in game.Sources.OfType<GalgameFolderSource>().Where(s => s.SaveMetaBackup))
        {
            if (targetSource is not null && source != targetSource) continue; //如果指定了目标源，则只保存到该源
            var folderPath = source.GetPath(game)!;
            var metaPath = Path.Combine(folderPath, ".PotatoVN");
            FolderBaseSaveMeta(game, metaPath);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 保存游戏的元数据到指定的.PotatoVN文件夹中
    /// </summary>
    /// <param name="targetGame">这个targetGame是const的，函数内部不会修改它</param>
    /// <param name="pvnPath">要保存的文件夹的位置</param>
    public static void FolderBaseSaveMeta(Galgame targetGame, string pvnPath) 
    {
        if (!Directory.Exists(pvnPath)) Directory.CreateDirectory(pvnPath);
        IFileService fileService = App.GetService<IFileService>();
        Galgame meta = targetGame.DeepClone();
        // 备份图片
        if (Utils.IsImageValid(meta.ImagePath.Value) &&
            FileHelper.CopyImg(meta.ImagePath.Value, pvnPath, $"{meta.Name.Value}_cover")?.Name is { } fileName) 
        {
            meta.ImagePath.ForceSet(Path.Combine(".", fileName));
        }
        foreach (GalgameCharacter character in meta.Characters)
        {
            if (Utils.IsImageValid(character.ImagePath))
            {
                FileHelper.CopyImg(character.ImagePath, pvnPath);
                character.ImagePath = Path.Combine(".", Path.GetFileName(character.ImagePath));
            }
            if (Utils.IsImageValid(character.PreviewImagePath))
            {
                FileHelper.CopyImg(character.PreviewImagePath, pvnPath);
                character.PreviewImagePath = Path.Combine(".", Path.GetFileName(character.PreviewImagePath));
            }
        }
        if (Utils.IsImageValid(meta.HeaderImagePath.Value))
        {
            FileHelper.CopyImg(meta.HeaderImagePath.Value, pvnPath, $"{meta.Name.Value}_Header");
            meta.HeaderImagePath.ForceSet(Path.Combine(".", Path.GetFileName(meta.HeaderImagePath.Value!)));
        }
        fileService.Save(pvnPath, "meta.json", meta);
    }

    /// <summary>
    /// 从指定的.potatovn文件夹中加载元数据，若加载失败则抛异常或返回null
    /// </summary>
    /// <param name="pvnPath"></param>
    /// <returns></returns>
    public static Galgame? FolderBaseLoadMeta(string pvnPath)
    {
        if (!Directory.Exists(pvnPath)) return null; // 不存在备份文件夹
        IFileService fileService = App.GetService<IFileService>();
        Galgame meta = fileService.Read<Galgame>(pvnPath, "meta.json")!;
        if (meta is null) throw new PvnException("meta.json not exist");
        _ = meta.Uid; //可能读到旧版本的导出文件，确保Ids的长度被正确新增为新版本的长度
        meta.ImagePath.ForceSet(FileHelper.LoadImg(meta.ImagePath.Value, pvnPath));
        meta.HeaderImagePath.ForceSet(FileHelper.LoadImg(meta.HeaderImagePath.Value, pvnPath));
        foreach (GalgameCharacter character in meta.Characters)
        {
            character.ImagePath = FileHelper.LoadImg(character.ImagePath, pvnPath)!;
            character.PreviewImagePath = FileHelper.LoadImg(character.PreviewImagePath, pvnPath)!;
        }
        meta.ExePath = FileHelper.LoadImg(meta.ExePath, pvnPath, defaultReturn: null);
        meta.SavePath = Directory.Exists(meta.SavePath) ? meta.SavePath : null; //检查存档路径是否存在并设置SavePosition字段
        meta.FindSaveInPath();
        return meta;
    }

    public async Task<Galgame?> LoadMetaAsync(string path)
    {
        await Task.CompletedTask;
        var metaFolderPath = Path.Combine(path, ".PotatoVN");
        return FolderBaseLoadMeta(metaFolderPath);
    }

    public Task RemoveMetaAsync(Galgame game)
    {
        return Task.Run(() =>
        {
            foreach (GalgameFolderSource source in game.Sources.OfType<GalgameFolderSource>())
            {
                try
                {
                    var folderPath = source.GetPath(game)!;
                    var metaPath = Path.Combine(folderPath, ".PotatoVN");
                    if (!Directory.Exists(metaPath)) return;
                    Directory.Delete(metaPath, true);
                    _infoService.Log(msg: $"[LocalFolderSourceService] remove meta folder {metaPath}");
                }
                catch (Exception e)
                {
                    _infoService.DeveloperEvent(msg: $"failed to remove meta folder with exception: {e}");
                }
            }
        });
    }

    public async Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source)
    {
        await Task.CompletedTask;
        try
        {
            DriveInfo? info = GetDriveInfo(source.Path);
            if (info is null) return (-1, -1);
            return (info.TotalSize, info.TotalSize - info.AvailableFreeSpace);
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: $"failed to get drive info with exception: {e}");
            return (-1, -1);
        }
    }

    public async Task AddListenAsync(GalgameSourceBase source)
    {
        if (source is not GalgameFolderSource folderSource)
            throw new ArgumentException($"source {source.Path} is not GalgameFolderSource");
        FileSystemWatcher watcher = new(folderSource.Path);
        watcher.NotifyFilter = NotifyFilters.DirectoryName;
        watcher.Filter = "*";
        watcher.EnableRaisingEvents = true;
        if (folderSource.DetectFolderAdd) watcher.Created += OnFolderCreated;
        if (folderSource.DetectFolderRemove) watcher.Deleted += OnFolderDelete;
        _watchers.Add(folderSource, watcher);
        await Task.CompletedTask;
    }

    public async Task RemoveListenAsync(GalgameSourceBase source)
    {
        if (source is not GalgameFolderSource folderSource) 
            throw new ArgumentException($"source {source.Path} is not GalgameFolderSource");
        if (_watchers.TryGetValue(folderSource, out FileSystemWatcher? watcher))
        {
            watcher.Dispose();
            _watchers.Remove(folderSource);
        }
        await Task.CompletedTask;
    }

    public string GetMoveInDescription(GalgameSourceBase target, string targetPath)
    {
        return "LocalFolderSourceService_MoveInDescription".GetLocalized(targetPath);
    }

    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame)
    {
        var path = target.GetPath(galgame) ?? string.Empty;
        return "LocalFolderSourceService_MoveOutDescription".GetLocalized(path);
    }

    public Task<string> GetSourcePathAsync(string gamePath) => Task.FromResult(Directory.GetParent(gamePath)!.FullName);

    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame)
    {
        if (moveIn?.SourceType == GalgameSourceType.LocalFolder)
            return moveOut?.SourceType == GalgameSourceType.LocalFolder
                ? null
                : "LocalFolderSourceService_MoveOutError".GetLocalized();
        return null;
    }

    public Task<string?> SelectPathInSourceAsync(GalgameSourceBase source) => FolderBaseSelectPathInSource(source);

    public static async Task<string?> FolderBaseSelectPathInSource(GalgameSourceBase source)
    {
        FolderPicker folderPicker = new();
        folderPicker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());
        StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
        if (folder is null) return null;
        if (!Utils.IsChildFolder(source.Path, folder.Path))
            throw new PvnException("LocalFolderSourceService_PathNotInSource".GetLocalized());
        return folder.Path;
    }

    private static DriveInfo? GetDriveInfo(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is null ? null : new DriveInfo(root);
    }

    private void OnFolderCreated(object sender, FileSystemEventArgs e)
    {
        if (!Directory.Exists(e.FullPath)) return;
        UiThreadInvokeHelper.Invoke(async () =>
        {
            try
            {
                IGalgameCollectionService gameService = App.GetService<IGalgameCollectionService>();
                await gameService.AddGameAsync(GalgameSourceType.LocalFolder, e.FullPath, true, false);
                _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Success,
                    "LocalFolderSourceService_OnFolderCreated".GetLocalized(), msg: e.FullPath);
            }
            catch (Exception exception)
            {
                _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                    "LocalFolderSourceService_OnFolderCreatedError".GetLocalized(), exception);
            }
        });
    }

    private void OnFolderDelete(object sender, FileSystemEventArgs e)
    {
        try
        {
            DirectoryInfo? sourceDir = new DirectoryInfo(e.FullPath).Parent;
            if (sourceDir is null)
            {
                Log($"Failed to get parent directory of {e.FullPath}", InfoBarSeverity.Warning);
                return;
            }
            IGalgameSourceCollectionService sourceService = App.GetService<IGalgameSourceCollectionService>();
            GalgameSourceBase? source =
                sourceService.GetGalgameSource(GalgameSourceType.LocalFolder, sourceDir.FullName);
            if (source is null)
            {
                Log($"Failed to get source from {sourceDir.FullName}");
                return;
            }

            GalgameAndPath? game = source.Galgames.FirstOrDefault(g => Utils.ArePathsEqual(g.Path, e.FullPath));
            if (game is null)
            {
                Log($"Failed to get game from {source.Path}");
                return;
            }

            sourceService.MoveOutNoOperate(source, game.Galgame);

            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Success,
                "LocalFolderSourceService_OnFolderDelete".GetLocalized(), msg: e.FullPath);
            Log($"Game {game.Galgame.Name} moved out from {source.Path}");
        }
        catch (Exception exception)
        {
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                "LocalFolderSourceService_OnFolderDeleteError".GetLocalized(), exception);
        }

        return;

        void Log(string msg, InfoBarSeverity severity = InfoBarSeverity.Informational) =>
            _infoService.Log(severity, msg: $"[OnFolderDelete] {msg}");
    }
}
