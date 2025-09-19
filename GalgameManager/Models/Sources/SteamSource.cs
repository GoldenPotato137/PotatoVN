using GalgameManager.Helpers;

namespace GalgameManager.Models.Sources;

//  对于steam source来说，其path为steamapps
public class SteamSource : GalgameSourceBase
{
    public override GalgameSourceType SourceType => GalgameSourceType.Steam;
    public override bool CanChangeScanOnStart => true;
    public override bool CanChangeCheckOnStart => true;
    public override bool CanChangeDetect => false;
    public override bool CanChangeSaveMetaBackup => true;
    public override bool IsGameAddable => false;
    public override bool IsSourceScanable => true;
    public override bool IsDelectable => true;

    public SteamSource() { }

    public SteamSource(string path)
    {
        DirectoryInfo dir = new(path);
        if (dir.Name != "steamapps") 
            throw new PvnException($"Steam source path is {path} which is not a valid steamapps folder.");
        Path = path;
        Name = "Steam";
    }

    public async override IAsyncEnumerable<(string? path, string msg)> ScanAllGalgames()
    {
        await Task.CompletedTask;
        DirectoryInfo dir = new(Path);
        if (!dir.Exists || dir.GetDirectories("common").FirstOrDefault() is not { } steamappsDir)
        {
            yield return (null, "SteamSource_ScanAllGalgames_InvalidPath".GetLocalized(Path));
            yield break;
        }
        foreach (DirectoryInfo gameDir in steamappsDir.GetDirectories())
            yield return (gameDir.FullName, "");
    }

    public override bool ApplySearchKey(string searchKey) => Path.ContainX(searchKey);

    public string MetaPath => System.IO.Path.Combine(Path, ".PotatoVN");
}