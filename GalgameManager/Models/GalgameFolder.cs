using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Services;

using Newtonsoft.Json;

namespace GalgameManager.Models;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public class GalgameFolder
{
    [JsonIgnore] public GalgameCollectionService GalgameService;

    [JsonIgnore] public bool IsRunning;
    [JsonIgnore] private int _progressValue;
    [JsonIgnore] private int _progressMax;
    [JsonIgnore] public string ProgressText = string.Empty;
    public event VoidDelegate? ProgressChangedEvent;

    public string Path
    {
        get;
        set;
    }

    public GalgameFolder(string path, IDataCollectionService<Galgame> service)
    {
        Path = path;
        GalgameService = ((GalgameCollectionService?)service)!;
    }

    public async Task<ObservableCollection<Galgame>> GetGalgameList()
    {
        var games = await GalgameService.GetContentGridDataAsync();
        return new ObservableCollection<Galgame>(games.Where(g => g.Path.StartsWith(Path)).ToList());
    }

    /// <summary>
    /// 扫描文件夹下的所有游戏并添加到库
    /// </summary>
    public async Task GetGalgameInFolder()
    {
        if (!Directory.Exists(Path) || IsRunning) return;
        IsRunning = true;
        _progressMax = Directory.GetDirectories(Path).Length;
        _progressValue = 0;
        var cnt = 0;
        foreach (var subPath in Directory.GetDirectories(Path))
        {
            _progressValue++;
            ProgressText = $"正在扫描路径:{subPath} , {_progressValue}/{_progressMax}";
            var result = await GalgameService.TryAddGalgameAsync(subPath);
            if (result == GalgameCollectionService.AddGalgameResult.Success) cnt++;
            ProgressChangedEvent?.Invoke();
        }

        ProgressText = $"扫描完成, 共添加了{cnt}个游戏";
        ProgressChangedEvent?.Invoke();
        await Task.Delay(3000);
        IsRunning = false;
        ProgressChangedEvent?.Invoke();
    }

    /// <summary>
    /// 从信息源更新库的所有游戏的信息
    /// </summary>
    public async Task GetInfoFromRss()
    {
        var galgames = await GetGalgameList();
        IsRunning = true;
        for(var i=0;i<galgames.Count;i++)
        {
            var galgame = galgames[i];
            ProgressText = $"正在获取 {galgame.Name} 的信息, {i}/{galgames.Count}";
            ProgressChangedEvent?.Invoke();
            await GalgameService.PhraseGalInfoAsync(galgame);
        }

        IsRunning = false;
        ProgressChangedEvent?.Invoke();
    }
}
