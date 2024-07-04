using GalgameManager.Helpers;
using GalgameManager.Models.Sources;

namespace GalgameManager.Models.BgTasks;

public class LocalFolderSourceMoveInTask : SourceMoveInBase
{
    public override string Title { get; } = "LocalFolderSourceMoveTask_MoveIn_Title".GetLocalized();

    public LocalFolderSourceMoveInTask(Galgame game, GalgameFolderSource targetSource, string targetPath) : base(game,
        targetSource,
        targetPath)
    {
    }

    protected override string ErrorEventTitle { get; init; } = "LocalFolderSourceTask_MoveIn_Error".GetLocalized();
    protected override string SuccessMsg() => "LocalFolderSourceTask_MoveIn_Success".GetLocalized(Game!.Name, TargetPath);

    protected async override Task RunIternal2Async()
    {
        await Task.CompletedTask;
        var originPath = Game!.Sources.FirstOrDefault(s => s.SourceType == GalgameSourceType.LocalFolder)
            ?.GetPath(Game);
        if (originPath is null) throw new PvnException("originPath is null");
        if (Utils.IsPathContained(originPath, TargetPath))
            throw new PvnException("TargetPath is contained in originPath");

        Queue<string> queue = new();
        queue.Enqueue(originPath);
        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var relativeP = Path.GetRelativePath(originPath, path);
            if (!Directory.Exists(Path.Combine(TargetPath, relativeP)))
                Directory.CreateDirectory(Path.Combine(TargetPath, relativeP));
            DirectoryInfo dir = new(path);
            foreach (FileInfo file in dir.GetFiles())
            {
                var targetFileP = Path.Combine(TargetPath, relativeP, file.Name);
                ChangeProgress(0, 1, "LocalFolderSourceTask_MoveIn_Progress".GetLocalized(file.Name, targetFileP));
                file.CopyTo(targetFileP, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
                queue.Enqueue(subDir.FullName);
        }
    }
}

public class LocalFolderSourceMoveOutTask : BgTaskBase
{
    protected override Task RecoverFromJsonInternal() => throw new NotImplementedException();

    protected override Task RunInternal() => throw new NotImplementedException();

    public override string Title { get; } = string.Empty;
}