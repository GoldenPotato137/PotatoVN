using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using H.NotifyIcon.Core;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameInfoFromRssTask : BgTaskBase
{
    public string GalgameSourceUrl = string.Empty;
    public IList<string> GalgamesName = new List<string>();
    private GalgameSourceBase? _galgameSource;
    private List<Galgame>? _galgames;


    public GetGalgameInfoFromRssTask() { }

    public GetGalgameInfoFromRssTask(GalgameSourceBase galgameSource, List<Galgame>? toUpdate = null)
    {
        _galgameSource = galgameSource;
        GalgameSourceUrl = galgameSource.Url;
        _galgames = toUpdate ?? (_galgameSource.GetGalgameList().Result).ToList();
        foreach (Galgame galgame in _galgames)
        {
            GalgamesName.Add(galgame.Name.Value ?? "");
        }
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgameSource = App.GetService<IGalgameSourceService>().GetGalgameSourceFromUrl(GalgameSourceUrl);
        _galgames = new List<Galgame>();
        foreach (var name in GalgamesName)
        {
            _galgames.Add(_galgameSource!.GetGalgameByName(name));
        }
        return Task.CompletedTask;
    }

    public override Task Run()
    {
        if (_galgameSource is null || _galgames is null || _galgameSource.IsRunning)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
        IBgTaskService bgTaskService = App.GetService<IBgTaskService>();
        var log = string.Empty;
        
        return Task.Run((async Task () =>
        {
            log += $"{DateTime.Now}\n{GalgameSourceUrl}\n\n";

            _galgameSource.IsRunning = true;
            var total = _galgames.Count;
            
            for (var i = 0;i<_galgames.Count;i++)
            {
                Galgame galgame = _galgames[i];
                ChangeProgress(i, total, $"正在获取 {galgame.Name.Value} 的信息");
                await UiThreadInvokeHelper.InvokeAsync(async Task() =>
                {
                    Galgame result = await galgameService.PhraseGalInfoAsync(galgame);
                    log += $"{result.Name} Done\n";
                });
            }
            
            ChangeProgress(0, 1, "GalgameFolder_GetGalgameInfo_Saving".GetLocalized());
            FileHelper.SaveWithoutJson(_galgameSource.GetLogName(), log, "Logs");
            await Task.Delay(1000); //等待文件保存
            ChangeProgress(1, 1, "GalgameFolder_GetGalgameInfo_Done".GetLocalized());
            _galgameSource.IsRunning = false;
            if (App.MainWindow is null && await localSettings.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder))
            {
                App.SystemTray?.ShowNotification(nameof(NotificationIcon.Info), 
                    "GalgameFolder_GetGalgameInfo_Done".GetLocalized());
            }
        })!);
    }

    public override bool OnSearch(string key) => GalgameSourceUrl.Contains(key);
    
    public override string Title { get; } = "GetGalgameInfoTask_Title".GetLocalized();
}