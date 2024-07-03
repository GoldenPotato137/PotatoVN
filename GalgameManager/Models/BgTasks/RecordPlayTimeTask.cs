using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Services;
using GalgameManager.ViewModels;

namespace GalgameManager.Models.BgTasks;

public class RecordPlayTimeTask : BgTaskBase
{
    private const int ManuallySelectProcessSec = 15; //认定为需要手动选择游戏进程的时间阈值
    
    public string ProcessName { get; set; } = string.Empty;
    public string GalgameUrl { get; set; }= string.Empty;
    public DateTime StartTime { get; set; }= DateTime.Now;
    public int CurrentPlayTime { get; set; } //本次游玩时间
    
    private Galgame? _galgame;
    private Process? _process;

    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();
    
    public RecordPlayTimeTask(){}

    public RecordPlayTimeTask(Galgame game, Process process)
    {
        Debug.Assert(game.CheckExistLocal());
        if (process.HasExited) return;
        ProcessName = process.ProcessName;
        GalgameUrl = game.Url;
        _galgame = game;
        _process = process;
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        _galgame = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)?.
            GetGalgameFromUrl(GalgameUrl);
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        if(_process is null || _galgame is null) return ;
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
                ChangeProgress(0, 1,
                    "RecordPlayTimeTask_Done".GetLocalized(_galgame.Name.Value ?? string.Empty,
                        TimeToDisplayTimeConverter.Convert(CurrentPlayTime)));
            });
            await (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!.SaveGalgamesAsync(_galgame);
            if(await App.GetService<ILocalSettingsService>().ReadSettingAsync<bool>(KeyValues.SyncGames))
                App.GetService<IPvnService>().Upload(_galgame, PvnUploadProperties.PlayTime);
        });
        
        _ = RecordPlayTimeAsync();

        await t;
    }

    private Task RecordPlayTimeAsync()
    {
        return Task.Run(() =>
        {
            var recordOnlyWhenForeground =
                _localSettingsService.ReadSettingAsync<bool>(KeyValues.RecordOnlyWhenForeground).Result;
            try
            {
                _localSettingsService.OnSettingChanged += OnSettingChanged;
                
                while (!_process!.HasExited)
                {
                    Thread.Sleep(1000 * 60);
                    if (_process.HasExited || 
                        (recordOnlyWhenForeground && (_process.IsMainWindowMinimized() || !_process.IsMainWindowActive())))
                        continue;
                    UiThreadInvokeHelper.Invoke(() =>
                    {
                        _galgame!.TotalPlayTime++;
                        CurrentPlayTime++;
                    });
                    var now = DateTime.Now.ToStringDefault();
                    if (!_galgame!.PlayedTime.TryAdd(now, 1))
                        _galgame.PlayedTime[now]++;
                }
            }
            finally
            {
                _localSettingsService.OnSettingChanged -= OnSettingChanged;
            }

            return;

            void OnSettingChanged(string key, object? value)
            {
                if(key != KeyValues.RecordOnlyWhenForeground || value is not bool b) return;
                recordOnlyWhenForeground = b;
            }
        });
    }

    public override string Title { get; } = "RecordPlayTimeTask_Title".GetLocalized();
}