using System.Collections;
using System.Collections.ObjectModel;
using GalgameManager.Contracts.Models;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Newtonsoft.Json;
using StdPath = System.IO.Path;

namespace GalgameManager.Models;


public class GalgameFolderSource : GalgameSourceBase
{
    [JsonIgnore] public bool IsUnpacking;
    public override SourceType GalgameSourceType => SourceType.LocalFolder;

    public GalgameFolderSource(string path, IDataCollectionService<Galgame> service): base(path, service)
    {
    }

    public override bool IsInSource(string path)
    {
        return path[..path.LastIndexOf('\\')] == Path ;
    }

    public override IEnumerator GetEnumerator()
    {
        
    };
}

public class GalgameFolderSourceIEnumerator : IEnumerator<Galgame>
{
    Queue<(string Path, int Depth)> pathToCheck = new();
    private GalgameFolderSource FolderSource;
    private List<string> FileShouldContain;
    private List<string> FileMustContain;
    private int MaxDepth = 1;
    public GalgameFolderSourceIEnumerator(GalgameFolderSource folderSource, List<string> fileShouldContain, List<string> fileMustContain, int maxDepth = 1)
    {
        FolderSource = folderSource;
        pathToCheck.Enqueue((FolderSource.Path, 0));
        FileShouldContain = fileShouldContain;
        FileMustContain = fileMustContain;
        MaxDepth = 1;
    }
    public bool MoveNext()
    {
        while (pathToCheck.Count > 0)
        {
            var (currentPath, currentDepth) = pathToCheck.Dequeue();
            if (!HasPermission(currentPath))
            {
                continue;
            }
            if (IsGameFolder(currentPath, FileMustContain, FileShouldContain))
            {
                Current;
            }
            else

            if (currentDepth == MaxDepth) continue;
            foreach (var subPath in Directory.GetDirectories(currentPath))
                pathToCheck.Enqueue((subPath, currentDepth + 1));
        }
    }
    
    private static bool HasPermission(string path)
    {
        try
        {
            Directory.GetFiles(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    private static bool IsGameFolder(string path, List<string> fileMustContain, List<string> fileShouldContain)
    {
        foreach(var file in fileMustContain)
            if (!Directory.GetFiles(path).Any(f => f.ToLower().EndsWith(file)))
                return false;
        var shouldContain = false;
        foreach(var file in fileShouldContain)
            if (Directory.GetFiles(path).Any(f => f.ToLower().EndsWith(file)))
            {
                shouldContain = true;
                break;
            }
        return shouldContain;
    }

    public void Reset() => throw new NotImplementedException();

    public Galgame Current { get; }

    object IEnumerator.Current => Current;

    public void Dispose() => throw new NotImplementedException();
}
