using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using H.NotifyIcon.Core;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameInfoFromRss : BgTaskBase
{
    public string GalgameSourceUrl = string.Empty;
    public IList<string> GalgamesName = new List<string>();
    private IGalgameSource? _galgameFolderSource;
    private List<Galgame>? _galgames;


    public GetGalgameInfoFromRss() { }

    public GetGalgameInfoFromRss(IGalgameSource galgameSource, List<Galgame>? toUpdate = null)
    {
        _galgameFolderSource = galgameSource;
        GalgameSourceUrl = galgameSource.Url;
        _galgames = toUpdate ?? (_galgameFolderSource.GetGalgameList().Result).ToList();
        foreach (Galgame galgame in _galgames)
        {
            GalgamesName.Add(galgame.Name.Value ?? "");
        }
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgameFolderSource = (App.GetService<IDataCollectionService<IGalgameSource>>() as GalgameSourceCollectionService)?.
            GetGalgameFolderFromUrl(GalgameSourceUrl);
        _galgames = new List<Galgame>();
        foreach (var name in GalgamesName)
        {
            _galgames.Add(_galgameFolderSource!.GetGalgameByName(name));
        }
        return Task.CompletedTask;
    }

    public override Task Run()
    {
        if (_galgameFolderSource is null || _galgames is null || Directory.Exists(GalgameSourceUrl) == false || _galgameFolderSource.IsRunning)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
        IBgTaskService bgTaskService = App.GetService<IBgTaskService>();
        var log = string.Empty;
        
        return Task.Run((async Task () =>
        {
            List<Task> characterTasks = new List<Task>();
            log += $"{DateTime.Now}\n{GalgameSourceUrl}\n\n";

            _galgameFolderSource.IsRunning = true;
            var total = _galgames.Count;
            
            for (var i = 0;i<_galgames.Count;i++)
            {
                Galgame galgame = _galgames[i];
                ChangeProgress(i, total, $"正在获取 {galgame.Name.Value} 的信息");
                await UiThreadInvokeHelper.InvokeAsync(async Task() =>
                {
                    Galgame result = await galgameService.PhraseGalInfoAsync(galgame);
                    log += $"{result.Name} Done";
                });
                characterTasks.Add(bgTaskService.AddBgTask(new GetGalgameCharactersFromRss(galgame)));
            }
            
            ChangeProgress(0, 1, "GalgameFolder_GetGalgameInfo_Saving".GetLocalized());
            FileHelper.SaveWithoutJson(_galgameFolderSource.GetLogName(), log, "Logs");
            await Task.Delay(1000); //等待文件保存
            ChangeProgress(1, 1, "GalgameFolder_GetGalgameInfo_Done".GetLocalized());
            _galgameFolderSource.IsRunning = false;
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