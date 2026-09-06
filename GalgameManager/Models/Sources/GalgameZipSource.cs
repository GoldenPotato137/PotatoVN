using System.Text.RegularExpressions;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using SystemPath = System.IO.Path;


namespace GalgameManager.Models.Sources;


public class GalgameZipSource : GalgameSourceBase
{
    public static string FxRegex = @"(?<name>[^\.\\]+)(?:(\.part1)?\.(zip|rar|7z|001))$";
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
        Match m = Regex.Match(fileName, FxRegex);
        return m.Success ? m.Groups["name"].Value : SystemPath.GetFileNameWithoutExtension(fileName);
    }

    public override bool IsInSource(string path)
    {
        return SystemPath.GetFullPath(path).StartsWith(SystemPath.GetFullPath(Path)) ;
    }

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
                // 显式过滤.meta备份目录下的文件（防御性）
                if (f.Contains($"{SystemPath.DirectorySeparatorChar}.PotatoVN{SystemPath.DirectorySeparatorChar}"))
                    continue;
                Match m = Regex.Match(f, FxRegex);
                if (m.Success)
                {
                    yield return (new (f), $"successfully add {f}\n");
                }

                yield return (null, $"{f} is not zip\n");

            }
            if (currentDepth == maxDepth) continue;
            foreach (var subPath in Directory.GetDirectories(currentPath))
                pathToCheck.Enqueue((subPath, currentDepth + 1));
        }
    }

    public override bool ApplySearchKey(string searchKey) => Path.ContainX(searchKey);
}
