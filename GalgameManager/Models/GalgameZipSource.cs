using System.Collections.ObjectModel;
using GalgameManager.Contracts.Models;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using Newtonsoft.Json;
using StdPath = System.IO.Path;

namespace GalgameManager.Models;


public class GalgameZipSource : GalgameSourceBase
{
    [JsonIgnore] public bool IsUnpacking;
    public override SourceType GalgameSourceType => SourceType.LocalZip;

    public GalgameZipSource(string path, IDataCollectionService<Galgame> service): base(path, service)
    {
    }

    public override bool IsInSource(string path)
    {
        return path[..path.LastIndexOf('\\')] == Path ;
    }

}
