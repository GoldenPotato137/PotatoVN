using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using H.NotifyIcon.Core;
using GalgameManager.ViewModels;
using GalgameManager.Views.Dialog;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameInSourceTask : BgTaskBase
{
    private readonly string _galgameSourceUrl = string.Empty;
    private GalgameSourceBase? _galgameFolderSource;

    public GetGalgameInSourceTask() { }

    public GetGalgameInSourceTask(GalgameSourceBase source)
    {
        _galgameFolderSource = source;
        _galgameSourceUrl = source.Url;
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgameFolderSource = App.GetService<IGalgameSourceCollectionService>().GetGalgameSourceFromUrl(_galgameSourceUrl);
        return Task.CompletedTask;
    }

    protected override Task RunInternal()
    {
        if (_galgameFolderSource is null || _galgameFolderSource.IsRunning)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!;
        ISourceScanResultService sourceScanResultService = App.GetService<ISourceScanResultService>(); // Placeholder for now
        INavigationService navigationService = App.GetService<INavigationService>(); // For navigation
        
        GalgameScanResult scanResult = new()
        {
            SourceId = _galgameFolderSource!.Id,
            SourceName = _galgameFolderSource.Name,
            ScanTime = DateTime.Now,
        };
        
        return Task.Run((async Task () =>
        {
            var ignoreFetchResult = await localSettings.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult);

            _galgameFolderSource.IsRunning = true;
            var cnt = 0;
            ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_GettingPaths".GetLocalized());
            HashSet<ExUri> dontScanPath = new(_galgameFolderSource.DontScanPath.Select(p => new ExUri(p)));
            await foreach (var (path, l) in  _galgameFolderSource.ScanAllGalgames()) 
            {
                PathScanResultItem itemResult = new() { Path = path ?? "N/A" };
                if (path == null || dontScanPath.Contains(new ExUri(path)))
                {
                    itemResult.ResultType = ScanResultType.Information;
                    itemResult.Message = path is null ? l : "GalgameFolder_GetGalInFolder_SkippedByDontScanPaths".GetLocalized();
                    scanResult.Results.Add(itemResult);
                    continue;
                }
                if (_galgameFolderSource.Galgames.FirstOrDefault(g => Utils.ArePathsEqual(g.Path, path)) is { } game) 
                {
                    itemResult.ResultType = ScanResultType.AlreadyExists;
                    itemResult.RelatedGameId = game.Galgame.Uuid;
                    scanResult.Results.Add(itemResult);
                    continue;
                }
                
                ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_Progress".GetLocalized(path));
                try
                {
                    await galgameService.AddGameAsync(_galgameFolderSource.SourceType, path, ignoreFetchResult, false);
                    cnt++;
                    itemResult.ResultType = ScanResultType.Success;
                }
                catch (Exception e)
                {
                    itemResult.ResultType = ScanResultType.Failed;
                    itemResult.Message = e is PvnException ? e.Message : e.ToString();
                }
                scanResult.Results.Add(itemResult);
            }
            ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_Saving".GetLocalized(cnt));
            sourceScanResultService.SaveScanResult(scanResult);
            var notify = !(cnt == 0 && await localSettings.ReadSettingAsync<bool>(KeyValues.EventGetGalgameInFolderEmpty) == false);
            ChangeProgress(1, 1, "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt, _galgameFolderSource.Name), notify);
            EventAction = () => navigationService.NavigateTo(typeof(ScanResultViewModel).FullName!, scanResult.SourceId);
            EventActionText = "GetGalgameInFolderTask_CheckResult".GetLocalized();
            _galgameFolderSource.IsRunning = false;
            if (App.MainWindow is null && await localSettings.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder))
            {
                App.SystemTray?.ShowNotification(nameof(NotificationIcon.Info), 
                    "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt, _galgameFolderSource.Name));
            }
        })!);
    }

    public override bool OnSearch(string key) => _galgameSourceUrl.Contains(key);

    public override string Title =>
        "GetGalgameInFolderTask_Title".GetLocalized(_galgameFolderSource?.Name ?? string.Empty);
}
