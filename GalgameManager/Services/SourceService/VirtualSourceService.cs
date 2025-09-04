using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;

namespace GalgameManager.Services;

public class VirtualSourceService : IGalgameSourceService
{
    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null) => BgTaskBase.Empty;

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game) => BgTaskBase.Empty;

    public Task SaveMetaAsync(Galgame game, GalgameSourceBase? targetSource = null) => Task.CompletedTask;

    public Task<Galgame?> LoadMetaAsync(string path) => Task.FromResult<Galgame?>(null);

    public Task RemoveMetaAsync(Galgame game) => Task.CompletedTask;

    public Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source) => 
        Task.FromResult((0L, 0L));

    public Task AddListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public Task RemoveListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public string GetMoveInDescription(GalgameSourceBase target, string targetPath) => string.Empty;

    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame) => string.Empty;
    public Task<string> GetSourcePathAsync(string gamePath) => Task.FromResult(string.Empty);

    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame) => null;
    public async Task<string?> SelectPathInSourceAsync(GalgameSourceBase source) 
    {
        await Task.CompletedTask;
        return null;
    }
}