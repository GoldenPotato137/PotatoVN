using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using H.NotifyIcon.Core;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameCharactersFromRssTask : BgTaskBase
{
    public string GalgamesName = "";
    private Galgame? _galgame;


    public GetGalgameCharactersFromRssTask() { }

    public GetGalgameCharactersFromRssTask(Galgame galgame)
    {
        _galgame = galgame;
        GalgamesName = galgame.Name.Value ?? "";
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgame =  (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)?.GetGalgameFromName(GalgamesName);
        return Task.CompletedTask;
    }

    protected override Task RunInternal()
    {
        if (_galgame is null)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!;
        var log = string.Empty;
        
        return Task.Run((async Task () =>
        {
            log += $"{DateTime.Now}\n{_galgame.Name}\n\n";
            var total = _galgame.Characters.Count;
            for (var i = 0; i < _galgame.Characters.Count; i++)
            {
                GalgameCharacter character = _galgame.Characters[i];
                ChangeProgress(i, total, 
                    "Galgame_GetCharacterInfo_GettingInfo".GetLocalized(character.Name, _galgame.Name.Value??""));
                await UiThreadInvokeHelper.InvokeAsync(async Task() =>
                {
                    character = await galgameService.PhraseGalCharacterAsync(character, _galgame.RssType);
                });
                log += $"{_galgame.Name.Value}->{character.Name} Done";
                ChangeProgress(i+1, total, 
                    "Galgame_GetCharacterInfo_GottenInfo".GetLocalized(character.Name, _galgame.Name.Value??""));

            }
            
            await galgameService.SaveGalgamesAsync();
            
            ChangeProgress(0, 1, "Galgame_GetCharacterInfo_Saving".GetLocalized());
            FileHelper.SaveWithoutJson(_galgame.GetLogName(), log, "Logs");
            await Task.Delay(1000); //等待文件保存

            ChangeProgress(1, 1, "Galgame_GetCharacterInfo_Done".GetLocalized(_galgame.Name.Value ?? string.Empty));
            if (App.MainWindow is null && await localSettings.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder))
            {
                App.SystemTray?.ShowNotification(nameof(NotificationIcon.Info),
                    "Galgame_GetCharacterInfo_Done".GetLocalized(_galgame.Name.Value ?? string.Empty));
            }
        })!);
    }

    public override bool OnSearch(string key) => _galgame?.Url.Contains(key) ?? false;
    
    public override string Title { get; } = "GetCharacterInfoTask_Title".GetLocalized();
}