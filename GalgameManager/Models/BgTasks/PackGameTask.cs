using GalgameManager.Helpers;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace GalgameManager.Models.BgTasks;

public class PackGameTask : BgTaskBase
{
    public string GamePath;
    public string ZipPath;

    public PackGameTask(string gamePath, string zipPath)
    {
        GamePath = gamePath;
        ZipPath = zipPath;
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal()
    {
        if (Path.Exists(ZipPath))
        {
            if (StartFromBg) File.Delete(ZipPath);
            else throw new PvnException("PackGameTask_ZipExist".GetLocalized(ZipPath));
        }
        if (!Path.Exists(GamePath))
            throw new PvnException("PackGameTask_GamePathNotExist".GetLocalized(GamePath));

        return Task.Run(() =>
        {
            using ZipArchive archive = ZipArchive.Create();
            archive.AddAllFromDirectory(GamePath);
            archive.SaveTo(ZipPath, CompressionType.Deflate);
        });
    }

    public override string Title => "PackGameTask_Title".GetLocalized();
}