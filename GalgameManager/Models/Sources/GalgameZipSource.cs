using System.Text.RegularExpressions;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using SystemPath = System.IO.Path;


namespace GalgameManager.Models.Sources;


public class GalgameZipSource : GalgameSourceBase
{
    /// 压缩包文件名：name[.partN].(zip|rar|7z)[.NNN] 或 name.NNN（分卷）
    private static readonly Regex PackRegex = new(
        @"^(?<name>.+?)(?:(?:\.part(?<part>\d+))?\.(?<ext>zip|rar|7z)(?:\.(?<vol>\d{3}))?|\.(?<vol>\d{3}))$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public override GalgameSourceType SourceType => GalgameSourceType.LocalZip;
    public override bool CanChangeScanOnStart => false;
    public override bool CanChangeCheckOnStart => true;
    public override bool CanChangeDetect => false;
    public override bool CanChangeSaveMetaBackup => true;
    public override bool IsGameAddable => false;
    public override bool IsSourceScanable => true;
    public override bool IsDelectable => true;

    public GalgameZipSource(string path): base(path)
    {
    }

    public GalgameZipSource()
    {

    }

    /// 从压缩包路径提取包名（如 "game.part1.zip" -> "game"）；不匹配时退化为去扩展名
    /// 也作为元数据备份文件夹名（库根/.PotatoVN/&lt;包名&gt;，见 ZipSourceService）
    public static string GetPackName(string packPath)
    {
        var fileName = SystemPath.GetFileName(packPath);
        return TryParsePack(fileName) is { } info ? info.Name : SystemPath.GetFileNameWithoutExtension(fileName);
    }

    /// 是否为可识别的压缩包，且是一组分卷中的第一卷（非分卷压缩包视为第一卷）
    public static bool IsFirstVolume(string packPath) =>
        TryParsePack(SystemPath.GetFileName(packPath)) is { IsFirstVolume: true };

    /// 获取压缩包的所有分卷（含自身）；非分卷压缩包只返回自身
    public static List<string> GetVolumePaths(string packPath)
    {
        List<string> result = [packPath];
        if (TryParsePack(SystemPath.GetFileName(packPath)) is not { IsMultiVolume: true } info) return result;
        var dir = SystemPath.GetDirectoryName(packPath);
        if (dir is null || !Directory.Exists(dir)) return result;
        foreach (var file in Directory.GetFiles(dir))
        {
            if (Utils.ArePathsEqual(file, packPath)) continue;
            if (TryParsePack(SystemPath.GetFileName(file)) is { } other && other.IsSameSeries(info))
                result.Add(file);
        }
        return result;
    }

    private static PackFileInfo? TryParsePack(string fileName)
    {
        Match m = PackRegex.Match(fileName);
        if (!m.Success) return null;
        return new PackFileInfo(m.Groups["name"].Value,
            m.Groups["ext"].Success ? m.Groups["ext"].Value.ToLowerInvariant() : null,
            m.Groups["part"].Success ? int.Parse(m.Groups["part"].Value) : null,
            m.Groups["vol"].Success ? int.Parse(m.Groups["vol"].Value) : null);
    }

    private readonly record struct PackFileInfo(string Name, string? Extension, int? Part, int? Volume)
    {
        public bool IsMultiVolume => Part is not null || Volume is not null;
        public bool IsFirstVolume => (Part is null or 1) && (Volume is null or 1);

        public bool IsSameSeries(PackFileInfo other) =>
            string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && Extension == other.Extension &&
            Part.HasValue == other.Part.HasValue && Volume.HasValue == other.Volume.HasValue;
    }

    public override bool IsInSource(string path) => Utils.IsPathContained(Path, path);

    public async override IAsyncEnumerable<(string?, string)> ScanAllGalgames()
    {
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        
        var searchSubFolder = await localSettings.ReadSettingAsync<bool>(KeyValues.SearchChildFolder);
        var maxDepth = searchSubFolder ? await localSettings.ReadSettingAsync<int>(KeyValues.SearchChildFolderDepth) : 1;
        
        Queue<(string Path, int Depth)> pathToCheck = new();
        pathToCheck.Enqueue((Path, 0));
        while (pathToCheck.Count > 0)
        {
            var (currentPath, currentDepth) = pathToCheck.Dequeue();

            foreach (var f in Directory.GetFiles(currentPath))
            {
                if (TryParsePack(SystemPath.GetFileName(f)) is not { } info)
                    yield return (null, $"{f} is not zip\n");
                else if (!info.IsFirstVolume) // 分卷压缩包只登记第一卷
                    yield return (null, $"{f} is not the first volume\n");
                else
                    yield return (f, $"successfully add {f}\n");
            }
            if (currentDepth == maxDepth) continue;
            foreach (var subPath in Directory.GetDirectories(currentPath))
            {
                // 跳过元数据备份目录
                if (SystemPath.GetFileName(subPath) == ".PotatoVN") continue;
                pathToCheck.Enqueue((subPath, currentDepth + 1));
            }
        }
    }

    public override bool ApplySearchKey(string searchKey) => Path.ContainX(searchKey);
}
