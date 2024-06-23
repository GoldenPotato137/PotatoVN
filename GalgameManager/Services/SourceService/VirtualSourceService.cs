using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Models.Sources;

namespace GalgameManager.Services;

public class VirtualSourceService : IGalgameSourceService
{
    public async Task MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null)
    {
        await Task.CompletedTask;
        target.AddGalgame(game, string.Empty);
    }

    public async Task MoveOutAsync(GalgameSourceBase target, Galgame game)
    {
        await Task.CompletedTask;
        target.DeleteGalgame(game);
    }
}