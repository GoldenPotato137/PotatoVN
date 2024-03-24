using System.Collections;
using System.Collections.ObjectModel;
using GalgameManager.Contracts.Models;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
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

    public override BgTaskBase GetGalgameInSourceTask()
    {
        return new GetGalgameInFolderTask(this);
    }
}

