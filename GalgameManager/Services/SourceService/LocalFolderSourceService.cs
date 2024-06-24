using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Models.Sources;

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

    public async Task MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null)
    {
        await Task.CompletedTask;
        if (targetPath is null)
        {
            _infoService.DeveloperEvent("targetPath is null");
            return;
        }

        target.AddGalgame(game, targetPath);
    }

    public async Task MoveOutAsync(GalgameSourceBase target, Galgame game)
    {
        await Task.CompletedTask;
        target.DeleteGalgame(game);
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
}