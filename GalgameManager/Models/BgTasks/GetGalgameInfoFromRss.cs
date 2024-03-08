using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using H.NotifyIcon.Core;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameInfoFromRss : BgTaskBase
{
    public string GalgameFolderPath = string.Empty;
    public IList<string> GalgamesName = new List<string>();
    private GalgameFolder? _galgameFolder;
    private List<Galgame>? _galgames;


    public GetGalgameInfoFromRss() { }

    public GetGalgameInfoFromRss(GalgameFolder folder, List<Galgame>? toUpdate = null)
    {
        _galgameFolder = folder;
        GalgameFolderPath = folder.Path;
        _galgames = toUpdate ?? (_galgameFolder.GetGalgameList().Result).ToList();
        foreach (Galgame galgame in _galgames)
        {
            GalgamesName.Add(galgame.Name.Value ?? "");
        }
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgameFolder = (App.GetService<IDataCollectionService<GalgameFolder>>() as GalgameFolderCollectionService)?.
            GetGalgameFolderFromPath(GalgameFolderPath);
        _galgames = new List<Galgame>();
        foreach (var name in GalgamesName)
        {
            _galgames.Add(_galgameFolder!.GetGalgameByName(name));
        }
        return Task.CompletedTask;
    }

    public override Task Run()
    {
        if (_galgameFolder is null || _galgames is null || Directory.Exists(GalgameFolderPath) == false || _galgameFolder.IsRunning)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
        IBgTaskService bgTaskService = App.GetService<IBgTaskService>();
        var log = string.Empty;
        
        return Task.Run((async Task () =>
        {
            List<Task> characterTasks = new List<Task>();
            log += $"{DateTime.Now}\n{GalgameFolderPath}\n\n";

            _galgameFolder.IsRunning = true;
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
            FileHelper.SaveWithoutJson(_galgameFolder.GetLogName(), log, "Logs");
            await Task.Delay(1000); //等待文件保存
            ChangeProgress(1, 1, "GalgameFolder_GetGalgameInfo_Done".GetLocalized());
            _galgameFolder.IsRunning = false;
            if (App.MainWindow is null && await localSettings.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder))
            {
                App.SystemTray?.ShowNotification(nameof(NotificationIcon.Info), 
                    "GalgameFolder_GetGalgameInfo_Done".GetLocalized());
            }
        })!);
    }

    public override bool OnSearch(string key) => GalgameFolderPath.Contains(key);
    
    public override string Title { get; } = "GetGalgameInfoTask_Title".GetLocalized();
}