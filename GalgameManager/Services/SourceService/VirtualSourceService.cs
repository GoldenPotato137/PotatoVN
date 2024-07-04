using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class VirtualSourceService : IGalgameSourceService
{
    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null)
    {
        target.AddGalgame(game, string.Empty);
        return BgTaskBase.Empty;
    }

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game)
    {
        target.DeleteGalgame(game);
        return BgTaskBase.Empty;
    }

    public Task SaveMetaAsync(Galgame game) => Task.CompletedTask;
    
    public Task<Galgame?> LoadMetaAsync(string path) => (Task<Galgame?>)Task.CompletedTask;

    public Task<Grid?> GetAdditionSettingControlAsync(GalgameSourceBase source,
        ChangeSourceDialogAttachSetting setting)
    {
        return Task.FromResult<Grid?>(null);
    }

    public Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source)
    {
        return Task.FromResult((-1L, -1L));
    }

    public string GetMoveInDescription(GalgameSourceBase target, string targetPath) => string.Empty;

    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame) => string.Empty;
}