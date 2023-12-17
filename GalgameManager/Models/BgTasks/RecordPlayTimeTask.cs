using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using GalgameManager.ViewModels;
using Newtonsoft.Json;

namespace GalgameManager.Models.BgTasks;

public class RecordPlayTimeTask : BgTaskBase
{
    [JsonIgnore] public static bool RecordOnlyWhenForeground; //是否只在游戏处于前台时记录游玩时间
    
    public string ProcessName = string.Empty;
    public string GalgamePath = string.Empty;
    private Galgame? _galgame;
    private Process? _process;
    
    public RecordPlayTimeTask(){}

    public RecordPlayTimeTask(Galgame game, Process process)
    {
        ProcessName = process.ProcessName;
        GalgamePath = game.Path;
        _galgame = game;
        _process = process;
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        _galgame = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)?.
            GetGalgameFromPath(GalgamePath);
        return Task.CompletedTask;
    }

    public override Task Run()
    {
        if(_process is null || _galgame is null) return Task.CompletedTask;
        if (StartFromBg)
        {
            Task.Run(async () =>
            {
                await _process.WaitForExitAsync();
                await UiThreadInvokeHelper.InvokeAsync(() =>
                {
                    App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, _galgame);
                    App.SetWindowMode(WindowMode.Normal);
                });
            });
        }
        return Task.Run(() =>
        {
            while (_process.HasExited == false)
            {
                Thread.Sleep(1000 * 60);
                if (_process.HasExited || 
                    (RecordOnlyWhenForeground && (_process.IsMainWindowMinimized() || !_process.IsMainWindowActive())))
                    continue;
                UiThreadInvokeHelper.Invoke(() =>
                {
                    _galgame.TotalPlayTime++;
                });
                var now = DateTime.Now.ToString("yyyy/M/d");
                if (_galgame.PlayedTime.TryAdd(now, 1) == false)
                    _galgame.PlayedTime[now]++;
            }
        });
    }
}