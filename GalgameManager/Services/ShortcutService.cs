using GalgameManager.Models;
using GalgameManager.Helpers;
using GalgameManager.Contracts.Services;
using System.Text;
using LiteDB;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Models.BgTasks;
using System.Diagnostics;
using GalgameManager.Enums;
using System.Collections.ObjectModel;
namespace GalgameManager.Services;
public  class ShortcutService : IShortcutService
{
    public string path_to_shortcut;
    private readonly Collection<Galgame> list;
    private readonly Dictionary<ulong,Shortcut> _all_shortcuts=new();
    private readonly Dictionary<ulong, Galgame> gals=new();
    private readonly Dictionary<ulong,Shortcut> scs=new();
    private readonly ILocalSettingsService _localSettingsService=App.GetService<ILocalSettingsService>();
    private readonly IGalgameCollectionService _galgameCollectionService=App.GetService<IGalgameCollectionService>();
    public ShortcutService()
    {
        path_to_shortcut = _localSettingsService.ReadSettingAsync<string>(KeyValues.PathToShortcut).Result;
        if (path_to_shortcut==""||path_to_shortcut==null)
        {
            throw new NotImplementedException("请填写shortcuts.vdf");
            return;
        }
        list = _galgameCollectionService.Galgames;
        foreach(Galgame g in list)
        {
            if(g.IsLocalGame)
            {
                if (g.IsSteam)
                {
                    gals[g.AppID]= g;
                }
            }
        }
        List<Shortcut> sc_list= ShortcutHelper.ShortcutReader.ReadShortcuts(path_to_shortcut);
        foreach(Shortcut s in sc_list)
        {
            _all_shortcuts[s.AppID] = s;
        }
        foreach (Shortcut shortcut in sc_list)
        {
            if (gals.ContainsKey(shortcut.AppID))
            {
                scs[shortcut.AppID] = shortcut;
            }
        }
    }

    public async Task AddShortcutAsync(Galgame galgame)
    {
        if (string.IsNullOrEmpty(path_to_shortcut))
        {
            throw new NotImplementedException("请填写shortcuts.vdf");
        }
        else
        {
            if(galgame.IsSteam)
            {
                
                throw new NotImplementedException("已加入steam");
            }
            else
            {
                Shortcut sc = Galgame_to_shortcut(galgame);
                galgame.AppID=ShortcutHelper.ShortcutWriter.Add_no_steam_game(sc, path_to_shortcut);
                galgame.IsSteam = true;
                gals[galgame.AppID] = galgame;
                scs[galgame.AppID] = sc;
            }
        }
        await Task.CompletedTask;
    }

    public async Task GetShortcutsAsync()
    {
        List<Shortcut> sc_list = ShortcutHelper.ShortcutReader.ReadShortcuts(path_to_shortcut);
        foreach (Shortcut s in sc_list)
        {
            _all_shortcuts[s.AppID] = s;
        }
        foreach (Shortcut shortcut in sc_list)
        {
            if (gals.ContainsKey(shortcut.AppID))
            {
                scs[shortcut.AppID] = shortcut;
            }
        }
        await Task.CompletedTask;
    }
    public void UpdateShortcutAsync()
    {
        var fileStream = new FileStream(path_to_shortcut, FileMode.Open, FileAccess.Read);
        var reader = new BinaryReader(fileStream, Encoding.UTF8);
        foreach(Shortcut sc in scs.Values)
        {
            if (sc.Check_Appid(reader)){
                reMatch();
                break;
            }
            if (sc.Check_LastPlayTime(reader))
            {
                Galgame galgame = gals[sc.AppID];
                Process process = Process.GetProcessesByName(galgame.ProcessName).FirstOrDefault();
                if (process == null)
                {
                    throw new InvalidOperationException("游戏进程未找到");
                }

                // 创建并启动记录游玩时间的任务
                var recordPlayTimeTask = new RecordPlayTimeTask(galgame, process);
                recordPlayTimeTask.StartTime = DateTime.Parse(sc.LastPlayTime);
                Task.Run(() => recordPlayTimeTask.Run());
                return;
            }
        }
    }
    public Shortcut Galgame_to_shortcut(Galgame game)
    {
        if (game.IsLocalGame && game.ExePath != null)
        {
            Shortcut sc = new Shortcut(_all_shortcuts.Count,game.Name, "\"" + game.ExePath + "\"", game.LocalPath + "\\", "");
            return sc;
        }
        throw new NotImplementedException("只能是本地游戏，请查看启动文件是否填写");
    }
    public void reMatch() {
        List<Shortcut> sc_list = ShortcutHelper.ShortcutReader.ReadShortcuts(path_to_shortcut);
        _all_shortcuts.Clear();
        foreach (Shortcut s in sc_list)
        {
            _all_shortcuts[s.AppID] = s;
        }
        foreach(Galgame g in gals.Values)
        {
            if (!scs.ContainsKey(g.AppID))
            {
                g.IsSteam = false;
                gals.Remove(g.AppID);
            }
        }
        scs.Clear();
        foreach(Shortcut shortcut in sc_list)
        {
            if (gals.ContainsKey(shortcut.AppID))
            {
                scs[shortcut.AppID] = shortcut;
            }
        }
    }
}