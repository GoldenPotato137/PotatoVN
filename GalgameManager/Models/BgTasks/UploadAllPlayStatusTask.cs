using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class UploadAllPlayStatusTask : QueueTaskBase<Galgame>
{
    private readonly bool _uploadBangumi;
    private readonly bool _uploadVndb;
    private readonly IGalgameCollectionService _gameService;
    private readonly IInfoService _infoService;

    public UploadAllPlayStatusTask(bool uploadBangumi, bool uploadVndb,
        IGalgameCollectionService gameService, IInfoService infoService)
    {
        _uploadBangumi = uploadBangumi;
        _uploadVndb = uploadVndb;
        _gameService = gameService;
        _infoService = infoService;
    }

    public void AddGalgame(Galgame? game)
    {
        if (game is null) return;
        Queue.Enqueue(game);
        UpdateProgressMsg();
    }

    public override string Title => "UploadAllPlayStatusTask_Title".GetLocalized();

    protected async override Task InitializeAsync()
    {
        // Enqueue all games that have play status != None
        foreach (Galgame g in _gameService.Galgames)
        {
            if (g.PlayType != PlayType.None)
                Queue.Enqueue(g);
        }
        UpdateProgressMsg();
        await Task.CompletedTask;
    }

    protected async override Task ProcessItemAsync(Galgame item)
    {
        try
        {
            if (_uploadBangumi)
            {
                try
                {
                    await _gameService.UploadPlayStatusAsync(item, RssType.Bangumi);
                }
                catch (Exception e)
                {
                    _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                        "UploadAllPlayStatusTask_UploadBgmFailed".GetLocalized(item.Name.Value ?? string.Empty), e);
                }
            }

            if (_uploadVndb)
            {
                try
                {
                    await _gameService.UploadPlayStatusAsync(item, RssType.Vndb);
                }
                catch (Exception e)
                {
                    _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                        "UploadAllPlayStatusTask_UploadVndbFailed".GetLocalized(item.Name.Value ?? string.Empty), e);
                }
            }
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                "UploadAllPlayStatusTask_GameFailed".GetLocalized(item.Name.Value ?? string.Empty), e);
        }
    }

    protected override string ProgressTitle() => "UploadAllPlayStatusTask_Progress_Title";

    protected override string ProgressMsg(Galgame item) => item.Name.Value ?? string.Empty;

    protected override string ProgressWaitingMsg() => "UploadAllPlayStatusTask_Progress_Waiting";
}