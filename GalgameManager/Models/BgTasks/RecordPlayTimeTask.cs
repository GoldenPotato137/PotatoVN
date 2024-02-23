using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using GalgameManager.ViewModels;
using Newtonsoft.Json;

namespace GalgameManager.Models.BgTasks;

public class RecordPlayTimeTask : BgTaskBase
{
    private const int ManuallySelectProcessSec = 15; //认定为需要手动选择游戏进程的时间阈值
    [JsonIgnore] public static bool RecordOnlyWhenForeground; //是否只在游戏处于前台时记录游玩时间
    
    public string ProcessName = string.Empty;
    public string GalgamePath = string.Empty;
    public DateTime StartTime = DateTime.Now;
    private Galgame? _galgame;
    private Process? _process;
    
    public RecordPlayTimeTask(){}

    public RecordPlayTimeTask(Galgame game, Process process)
    {
        if (process.HasExited) return;
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
        ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(_galgame.Name.Value!));
        Task t = Task.Run(async () =>
        {
            await _process.WaitForExitAsync();
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                GalgamePageParameter parma = new()
                {
                    Galgame = _galgame,
                    SelectProgress = DateTime.Now - StartTime < TimeSpan.FromSeconds(ManuallySelectProcessSec) 
                                     && _galgame.ProcessName is null
                };
                App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, parma);
                App.SetWindowMode(WindowMode.Normal);
            });
            await (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!.SaveGalgamesAsync(_galgame);
            App.GetService<IPvnService>().Upload(_galgame, PvnUploadProperties.PlayTime);
        });
        
        Task.Run(() =>
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
                var now = DateTime.Now.ToStringDefault();
                if (_galgame.PlayedTime.TryAdd(now, 1) == false)
                    _galgame.PlayedTime[now]++;
            }
        });
        
        return t;
    }

    public override string Title { get; } = "RecordPlayTimeTask_Title".GetLocalized();
}