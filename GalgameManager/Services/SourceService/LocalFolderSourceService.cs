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

    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null,
        GalgameAndPath? sourceEntry = null)
    {
        if (target is not GalgameFolderSource) throw new ArgumentException("target is not GalgameFolderSource");
        sourceEntry ??= game.PreferredLocalInstallation;
        if (sourceEntry is null) throw new PvnException("source installation is null");
        targetPath ??= GetMoveInPath(target, sourceEntry);
        // 来源为压缩包时解压到目标位置，否则复制游戏文件夹
        if (sourceEntry.Source is GalgameZipSource)
            return new UnpackGameTask(sourceEntry.Path, Path.GetDirectoryName(targetPath)!,
                Path.GetFileName(targetPath)) { AddToLibrary = false };
        return new LocalFolderSourceMoveInTask(game, sourceEntry.Path, targetPath);
    }

    /// 移入本地库时游戏的默认存放位置：&lt;库根&gt;/&lt;来源文件夹名或压缩包名&gt;
    public static string GetMoveInPath(GalgameSourceBase target, GalgameAndPath sourceEntry)
    {
        var name = sourceEntry.Source is GalgameZipSource
            ? GalgameZipSource.GetPackName(sourceEntry.Path)
            : new DirectoryInfo(sourceEntry.Path).Name;
        return Path.Combine(target.Path, name.RemoveInvalidChars());
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
            FolderBaseSaveMeta(game, metaPath, source.GetEntry(game)?.LocalConfig);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 保存游戏的元数据到指定的.PotatoVN文件夹中
    /// </summary>
    /// <param name="targetGame">这个targetGame是const的，函数内部不会修改它</param>
    /// <param name="pvnPath">要保存的文件夹的位置</param>
    /// <param name="installationConfig">当前目录对应的安装实例配置</param>
    public static void FolderBaseSaveMeta(Galgame targetGame, string pvnPath,
        LocalInstallationConfig? installationConfig = null)
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
        fileService.Save(pvnPath, "meta.json", new GameMetaBackup
        {
            Version = GameMetaBackup.CurrentVersion,
            Game = meta,
            Installation = installationConfig?.Clone(),
        });
    }

    /// <summary>
    /// 从指定的.potatovn文件夹中加载元数据，若加载失败则抛异常或返回null
    /// </summary>
    /// <param name="pvnPath">.PotatoVN元数据目录</param>
    /// <param name="installationPath">当前安装实例根目录</param>
    /// <returns>加载出的逻辑游戏元数据</returns>
    public static Galgame? FolderBaseLoadMeta(string pvnPath, string installationPath)
    {
        if (!Directory.Exists(pvnPath)) return null; // 不存在备份文件夹
        IFileService fileService = App.GetService<IFileService>();
        GameMetaBackup? backup = fileService.Read<GameMetaBackup>(pvnPath, "meta.json");
        Galgame? meta = backup is { Version: >= GameMetaBackup.CurrentVersion, Game: not null }
            ? backup.Game
            : fileService.Read<Galgame>(pvnPath, "meta.json");
        if (meta is null) throw new PvnException("meta.json not exist");
        _ = meta.Uid; //可能读到旧版本的导出文件，确保Ids的长度被正确新增为新版本的长度
        meta.ImagePath.ForceSet(FileHelper.LoadImg(meta.ImagePath.Value, pvnPath));
        meta.HeaderImagePath.ForceSet(FileHelper.LoadImg(meta.HeaderImagePath.Value, pvnPath));
        foreach (GalgameCharacter character in meta.Characters)
        {
            character.ImagePath = FileHelper.LoadImg(character.ImagePath, pvnPath)!;
            character.PreviewImagePath = FileHelper.LoadImg(character.PreviewImagePath, pvnPath)!;
        }
        LocalInstallationConfig localConfig = backup?.Installation
            ?? meta.CreateLegacyLocalConfiguration(installationPath);
        localConfig.ExePath = FileHelper.LoadImg(localConfig.ExePath, pvnPath, defaultReturn: null);
        localConfig.SavePath = Directory.Exists(localConfig.SavePath) ? localConfig.SavePath : null;
        if (localConfig.DetectedSavePath is { } detected)
            localConfig.DetectedSavePath = detected.Relocated(installationPath);
        meta.LoadLegacyLocalConfiguration(localConfig);
        return meta;
    }

    public async Task<Galgame?> LoadMetaAsync(string path)
    {
        await Task.CompletedTask;
        var metaFolderPath = Path.Combine(path, ".PotatoVN");
        return FolderBaseLoadMeta(metaFolderPath, path);
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

    public string GetMoveInDescription(GalgameSourceBase target, Galgame galgame, GalgameAndPath? sourceEntry)
    {
        if (sourceEntry is null) return string.Empty;
        var targetPath = GetMoveInPath(target, sourceEntry);
        return sourceEntry.Source is GalgameZipSource
            ? "LocalFolderSourceService_UnpackInDescription".GetLocalized(sourceEntry.Path, targetPath)
            : "LocalFolderSourceService_MoveInDescription".GetLocalized(targetPath);
    }

    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame, bool deleteFiles)
    {
        var path = target.GetPath(galgame) ?? string.Empty;
        return deleteFiles
            ? "LocalFolderSourceService_MoveOutDescription".GetLocalized(path)
            : "LocalFolderSourceService_UnlinkDescription".GetLocalized(path);
    }

    public Task<string> GetSourcePathAsync(string gamePath) => Task.FromResult(Directory.GetParent(gamePath)!.FullName);

    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame)
    {
        if (moveIn is not GalgameFolderSource target) return null;
        GalgameAndPath? sourceEntry = SourceMoveTask.ResolveSourceEntry(galgame, moveOut);
        var sourceExists = sourceEntry?.Source is GalgameZipSource
            ? File.Exists(sourceEntry.Path)
            : sourceEntry is not null && Directory.Exists(sourceEntry.Path);
        if (!sourceExists) return "LocalFolderSourceService_NoSourceToCopy".GetLocalized();
        var targetPath = GetMoveInPath(target, sourceEntry!);
        return Path.Exists(targetPath) ? "LocalFolderSourceService_TargetExists".GetLocalized(targetPath) : null;
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

    private async void OnFolderDelete(object sender, FileSystemEventArgs e)
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

            await sourceService.MoveOutNoOperate(game);

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
