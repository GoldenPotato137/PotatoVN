using System.Collections.ObjectModel;

using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;


namespace GalgameManager.Services;

// ReSharper disable EnforceForeachStatementBraces
// ReSharper disable EnforceIfStatementBraces
public class GalgameCollectionService : IDataCollectionService<Galgame>
{
    private ObservableCollection<Galgame> _galgames = new();
    private ILocalSettingsService LocalSettingsService { get; }
    public delegate void GalgameAddedEventHandler(Galgame galgame);
    public event GalgameAddedEventHandler? GalgameAddedEvent;
    
    public GalgameCollectionService(ILocalSettingsService localSettingsService)
    {
        LocalSettingsService = localSettingsService;
        GetGalgames();
    }

    private async void GetGalgames()
    {
        _galgames = await LocalSettingsService.ReadSettingAsync<ObservableCollection<Galgame>>(KeyValues.Galgames) ?? new ObservableCollection<Galgame>();
        // _galgames.Add(new Galgame(@"D:\Game\魔女的夜宴"));
        // _galgames.Add(new Galgame(@"D:\Game\星光咖啡馆与死神之蝶"));
        // _galgames.Add(new Galgame(@"D:\Game\大图书馆的牧羊人"));
        //
        // foreach (var galgame in await GetGalFromFolder(@"D:\GalGame"))
        //     _galgames.Add(galgame);
        //
        // for(var i=0;i<_galgames.Count;i++)
        // {
        //     _galgames[i] = await PhraserAsync(_galgames[i], new BgmPhraser(LocalSettingsService));
        // }
    }

    /// <summary>
    /// 试图添加一个galgame，若已存在则不添加
    /// </summary>
    /// <param name="path">galgame路径</param>
    /// <param name="isForce">是否强制添加（若RSS源中找不到相关游戏信息）</param>
    public async Task TryAddGalgameAsync(string path, bool isForce = false)
    {
        if (_galgames.Any(gal => gal.Path == path))
            return;

        var galgame = new Galgame(path);
        galgame = await PhraserAsync(galgame, new BgmPhraser(LocalSettingsService));
        if(!isForce && galgame.RssType == RssType.None)
            return;
        _galgames.Add(galgame);
        GalgameAddedEvent?.Invoke(galgame);
        await LocalSettingsService.SaveSettingAsync(KeyValues.Galgames, _galgames);
    }
    
    private static async Task<Galgame> PhraserAsync(Galgame galgame, IGalInfoPhraser phraser)
    {
        var tmp = await phraser.GetGalgameInfo(galgame.Name);
        if (tmp == null) return galgame;

        galgame.Description = tmp.Description;
        galgame.Developer = tmp.Developer;
        galgame.Name = tmp.Name;
        galgame.RssType = phraser.GetPhraseType();
        return galgame;
    }
    
    public async Task<ObservableCollection<Galgame>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgames;
    }
}
