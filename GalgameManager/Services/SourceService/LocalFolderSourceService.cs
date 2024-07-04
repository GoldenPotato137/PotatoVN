using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class LocalFolderSourceService : IGalgameSourceService
{
    private readonly IInfoService _infoService;
    private readonly IFileService _fileService;

    public LocalFolderSourceService(IInfoService infoService, IFileService fileService)
    {
        _infoService = infoService;
        _fileService = fileService;
    }

    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null)
    {
        if (targetPath is null) throw new PvnException("targetPath is null");
        if (target is not GalgameFolderSource source) throw new ArgumentException("target is not GalgameFolderSource");
        return new LocalFolderSourceMoveInTask(game, source, targetPath);
    }

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game)
    {
        return new LocalFolderSourceMoveOutTask();
    }

    public async Task SaveMetaAsync(Galgame game)
    {
        foreach (GalgameFolderSource source in game.Sources.OfType<GalgameFolderSource>())
        {
            var folderPath = source.GetPath(game)!;
            var metaPath = Path.Combine(folderPath, ".PotatoVN");
            if (!Directory.Exists(metaPath)) Directory.CreateDirectory(metaPath);
            Galgame meta = game.GetMetaCopy(folderPath);
            var destImagePath = Path.Combine(metaPath, meta.ImagePath.Value!);
            _fileService.Save(metaPath, "meta.json", meta);
            // 备份图片
            CopyImg(game.ImagePath.Value, destImagePath);
            foreach (GalgameCharacter character in game.Characters)
            {
                var destCharPreviewImagePath = Path.Combine(metaPath, Path.GetFileName(character.PreviewImagePath));
                var destCharImagePath = Path.Combine(metaPath, Path.GetFileName(character.ImagePath));
                CopyImg(character.PreviewImagePath, destCharPreviewImagePath);
                CopyImg(character.ImagePath, destCharImagePath);
            }
        }

        await Task.CompletedTask;
    }

    public async Task<Galgame?> LoadMetaAsync(string path)
    {
        await Task.CompletedTask;
        var metaFolderPath = Path.Combine(path, "PotatoVN");
        Galgame meta = _fileService.Read<Galgame>(metaFolderPath, "meta.json")!;
        if (meta.Path.EndsWith('\\')) meta.Path = meta.Path[..^1];
        meta.ImagePath.ForceSet(LoadImg(meta.ImagePath.Value, metaFolderPath));
        foreach (GalgameCharacter character in meta.Characters)
        {
            character.ImagePath = LoadImg(character.ImagePath, metaFolderPath)!;
            character.PreviewImagePath = LoadImg(character.PreviewImagePath, metaFolderPath)!;
        }
        meta.UpdateIdFromMixed();
        meta.ExePath = LoadImg(meta.ExePath, metaFolderPath, defaultReturn: null);
        meta.SavePath = Directory.Exists(meta.SavePath) ? meta.SavePath : null; //检查存档路径是否存在并设置SavePosition字段
        meta.FindSaveInPath();
        return meta;
    }

    public async Task<Grid?> GetAdditionSettingControlAsync(GalgameSourceBase source,
        ChangeSourceDialogAttachSetting setting)
    {
        if(source is not GalgameFolderSource s) throw new ArgumentException("source is not GalgameFolderSource");

        setting.OkClickable = false;
        List<string> subFolders = await s.GetPossibleFoldersAsync();
        if (subFolders.Count <= 1) //只有一个文件夹（源的根），不需要选择
        {
            setting.OkClickable = true;
            return null;
        }

        Grid result = new();
        StackPanel panel = new() { Orientation = Orientation.Horizontal, Spacing = 20, };
        ComboBox box = new() { ItemsSource = subFolders, };
        box.SelectionChanged += (_, _) =>
        {
            setting.TargetPath = box.SelectedItem as string;
            setting.OkClickable = true;
        };
        panel.Children.Add(new TextBlock
        {
            Text = "LocalFolderSourceService_SelectFolder".GetLocalized(), 
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(box);
        result.Children.Add(panel);
        return result;
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

    public string GetMoveInDescription(GalgameSourceBase target, string targetPath)
    {
        return "LocalFolderSourceService_MoveInDescription".GetLocalized(targetPath);
    }

    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame)
    {
        var path = target.GetPath(galgame) ?? string.Empty;
        return "LocalFolderSourceService_MoveOutDescription".GetLocalized(path);
    }

    private static void CopyImg(string? src, string target)
    {
        if (src is null or Galgame.DefaultImagePath) return;
        if (!File.Exists(src) || File.Exists(target)) return;
        File.Copy(src, target);
    }

    private static string? LoadImg(string? target, string path, string defaultTarget = Galgame.DefaultImagePath, 
        string? defaultReturn = Galgame.DefaultImagePath)
    {
        if (string.IsNullOrEmpty(target) || target == defaultTarget) return defaultReturn;
        var targetPath = Path.GetFullPath(Path.Combine(path, target));
        return File.Exists(target) ? targetPath : defaultReturn;
    }
    
    private static DriveInfo? GetDriveInfo(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is null ? null : new DriveInfo(root);
    }
}