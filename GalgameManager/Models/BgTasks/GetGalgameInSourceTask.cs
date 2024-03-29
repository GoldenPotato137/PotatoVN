using GalgameManager.Contracts.Models;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using H.NotifyIcon.Core;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameInSourceTask : BgTaskBase
{
    public string GalgameSourceUrl = string.Empty;
    private GalgameSourceBase? _galgameFolderSource;

    public GetGalgameInSourceTask() { }

    public GetGalgameInSourceTask(GalgameSourceBase source)
    {
        _galgameFolderSource = source;
        GalgameSourceUrl = source.Url;
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgameFolderSource = (App.GetService<IDataCollectionService<GalgameSourceBase>>() as GalgameSourceCollectionService)?.
            GetGalgameSourceFromUrl(GalgameSourceUrl);
        return Task.CompletedTask;
    }

    public override Task Run()
    {
        //TODO
        if (_galgameFolderSource is null || _galgameFolderSource.IsRunning)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
        var log = string.Empty;
        
        return Task.Run((async Task () =>
        {
            log += $"{DateTime.Now}\n{GalgameSourceUrl}\n\n";
            var ignoreFetchResult = await localSettings.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult);

            _galgameFolderSource.IsRunning = true;
            var cnt = 0;
            await foreach ((Galgame? galgame, var l) in _galgameFolderSource.ScanAllGalgames())
            {
                if (galgame == null)
                {
                    log += l;
                    continue;
                }
                ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_Progress".GetLocalized(galgame.Path));
                AddGalgameResult result = AddGalgameResult.Other;
                await UiThreadInvokeHelper.InvokeAsync(async Task() =>
                {
                    result = await galgameService.TryAddGalgameAsync(galgame, ignoreFetchResult);
                    if (result == AddGalgameResult.Success ||
                        (ignoreFetchResult && result == AddGalgameResult.NotFoundInRss))
                        cnt++;
                });
                log += result;

            }
            ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_Saving".GetLocalized(cnt));
            FileHelper.SaveWithoutJson(_galgameFolderSource.GetLogName(), log, "Logs");
            await Task.Delay(1000); //等待文件保存
            
            ChangeProgress(1, 1, "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt));
            _galgameFolderSource.IsRunning = false;
            if (App.MainWindow is null && await localSettings.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder))
            {
                App.SystemTray?.ShowNotification(nameof(NotificationIcon.Info), 
                    "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt));
            }
        })!);
    }

    public override bool OnSearch(string key) => GalgameSourceUrl.Contains(key);
    
    public override string Title { get; } = "GetGalgameInFolderTask_Title".GetLocalized();
    
    
}