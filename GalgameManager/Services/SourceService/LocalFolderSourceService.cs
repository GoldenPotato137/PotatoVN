using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class LocalFolderSourceService : IGalgameSourceService
{
    private readonly IInfoService _infoService;

    public LocalFolderSourceService(IInfoService infoService)
    {
        _infoService = infoService;
    }

    public async Task MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null)
    {
        await Task.CompletedTask;
        if (targetPath is null)
        {
            _infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Warning,
                "UnexpectedEvent".GetLocalized(),
                new PvnException($"$Can not move game {{game.Name}} into source {{target.Path}}: targetPath is null"));
            return;
        }

        target.AddGalgame(game, targetPath);
    }

    public async Task MoveOutAsync(GalgameSourceBase target, Galgame game)
    {
        await Task.CompletedTask;
        target.DeleteGalgame(game);
    }
}