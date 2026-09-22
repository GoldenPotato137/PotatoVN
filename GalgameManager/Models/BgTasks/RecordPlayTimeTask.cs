using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.ViewModels;
using GalgameManager.WinApp.Base.Models.Msgs;

namespace GalgameManager.Models.BgTasks;

public class RecordPlayTimeTask : BgTaskBase, IDeduplicatedBgTask
{
    private const int ManuallySelectProcessSec = 15; //认定为需要手动选择游戏进程的时间阈值

    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }= DateTime.Now;
    public int CurrentPlayTime { get; set; } //本次游玩时间
    public Guid? InstallationId { get; set; } // 本次游玩使用的安装实例Id
    public override bool ProgressOnTrayIcon => true;
    public string? DeduplicationKey => Galgame?.Uuid.ToString("D");

    public Galgame? Galgame;
    private Process? _process;
    private string? _directoryPrefix; // 安装目录前缀，用于退出后探测新出现的游戏进程
    private HashSet<int>? _knownProcessIds; // 开始跟踪时已存在于安装目录的进程，复查时只附着新出现的进程
    private volatile bool _stopped;

    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();
    private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
    private int _minPlayTimeRecordThreshold;

    public RecordPlayTimeTask(){}

    /// <summary>
    /// 使用游戏的首选安装实例创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">要跟踪的游戏进程</param>
    public RecordPlayTimeTask(Galgame game, Process process)
        : this(game, process, game.PreferredInstallationId)
    {
    }

    /// <summary>
    /// 使用明确安装实例创建游玩时间记录任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="process">要跟踪的游戏进程</param>
    /// <param name="installationId">本次游玩使用的安装实例Id</param>
    public RecordPlayTimeTask(Galgame game, Process process, Guid? installationId)
    {
        Debug.Assert(game.IsLocalGame);
        try
        {
            if (process.HasExited) return;
            ProcessName = process.ProcessName;
        }
        catch
        {
            // 进程对象可能无效（例如通过ShellExecute启动的快捷方式），仍创建任务，由退出后的目录检查兜底
        }
        Galgame = game;
        _process = process;
        InstallationId = installationId;
        InitDirectoryWatch();
    }

    protected override Task RecoverFromJsonInternal()
    {
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        InitDirectoryWatch();
        return Task.CompletedTask;
    }

    private void InitDirectoryWatch()
    {
        string? path = Galgame?.SourceEntries.FirstOrDefault(e => e.EntryId == InstallationId)?.Path;
        if (string.IsNullOrEmpty(path)) return;
        _directoryPrefix = GameProcessDetector.GetDirectoryPrefix(path);
        _knownProcessIds = GameProcessDetector.GetProcessIdsInDirectory(_directoryPrefix);
    }

    protected async override Task RunInternal()
    {
        if(_process is null || Galgame is null) return ;
        ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
        Task t = Task.Run(async () =>
        {
            // 被跟踪的进程退出后，游戏目录内可能出现真正的游戏进程（启动器场景），尝试重新附着
            while (true)
            {
                try
                {
                    await _process.WaitForExitAsync();
                }
                catch
                {
                    // 进程对象无效（例如通过ShellExecute启动的快捷方式），直接进行目录检查
                }
                Process? next = await WaitForReplacementProcessAsync(GameProcessDetector.SafeGetId(_process));
                if (next is null) break;
                _process = next;
                ProcessName = next.ProcessName;
                _knownProcessIds?.Add(next.Id);
                ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
            }
            _stopped = true;
            var windowMode = await _localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode);
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                _gameService.SaveGalgameAsync(Galgame);
                GalgamePageParameter parma = new()
                {
                    Galgame = Galgame,
                    SelectProgress = DateTime.Now - StartTime < TimeSpan.FromSeconds(ManuallySelectProcessSec)
                                     && Galgame.SourceEntries.FirstOrDefault(e => e.EntryId == InstallationId)
                                         ?.LocalConfig?.ProcessName is null
                };
                if (windowMode == WindowMode.SystemTray)
                    App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, parma);
                App.SetWindowMode(WindowMode.Normal);
                ChangeProgress(1, 1,
                    "RecordPlayTimeTask_Done".GetLocalized(Galgame.Name.Value ?? string.Empty,
                        TimeToDisplayTimeConverter.Convert(CurrentPlayTime)));
                // 手动通知LastPlayTime属性已更改
                Galgame.RaisePropertyChanged(nameof(Galgame.LastPlayTime));
                if (CurrentPlayTime >= _minPlayTimeRecordThreshold) Galgame!.PlayCount++;
                App.GetService<IMessenger>().Send(new GalgameStoppedMessage(Galgame));
            });
            await App.GetService<IGalgameCollectionService>().SaveGalgameAsync(Galgame);
            if(await App.GetService<ILocalSettingsService>().ReadSettingAsync<bool>(KeyValues.SyncGames))
                App.GetService<IPvnService>().Upload(Galgame, PvnUploadProperties.PlayTime);
        });

        _ = RecordPlayTimeAsync();

        await t;
    }

    /// <summary>
    /// 启动器类游戏中，被跟踪进程退出后真正的游戏进程才刚被拉起。
    /// 退出后做5秒检查（每秒一次），若安装目录内出现了新进程则返回该进程。
    /// </summary>
    private async Task<Process?> WaitForReplacementProcessAsync(int exitedProcessId)
    {
        if (_directoryPrefix is null || _knownProcessIds is null) return null;
        _knownProcessIds.Add(exitedProcessId);
        for (int i = 0; i < 5; i++)
        {
            ChangeProgress(0, 1,
                "RecordPlayTimeTask_WaitingForProcess".GetLocalized(Galgame!.Name.Value ?? string.Empty,
                    (5 - i).ToString()));
            await Task.Delay(1000);
            Process? candidate =
                GameProcessDetector.FindBestProcessInDirectory(_directoryPrefix, _knownProcessIds);
            if (candidate is not null) return candidate;
        }
        return null;
    }

    private Task RecordPlayTimeAsync()
    {
        return Task.Run(async () =>
        {
            var recordOnlyWhenForeground =
                await _localSettingsService.ReadSettingAsync<bool>(KeyValues.RecordOnlyWhenForeground);
            _minPlayTimeRecordThreshold = await _localSettingsService.ReadSettingAsync<int>(KeyValues.MinPlayTimeRecordThreshold);
            try
            {
                _localSettingsService.OnSettingChanged += OnSettingChanged;

                while (!_stopped)
                {
                    Thread.Sleep(1000 * 60);
                    Process? current = _process;
                    if (_stopped || current is null || !GameProcessDetector.IsAlive(current) ||
                        (recordOnlyWhenForeground && (current.IsMainWindowMinimized() || !current.IsMainWindowActive())))
                        continue;
                    UiThreadInvokeHelper.Invoke(() =>
                    {
                        Galgame!.TotalPlayTime++;
                        CurrentPlayTime++;
                        _gameService.SaveGalgameAsync(Galgame);
                    });
                    var now = DateTime.Now.ToStringDefault();
                    if (!Galgame!.PlayedTime.TryAdd(now, 1))
                        Galgame.PlayedTime[now]++;
                }
            }
            finally
            {
                _localSettingsService.OnSettingChanged -= OnSettingChanged;
            }

            return;

            void OnSettingChanged(string key, object? value)
            {
                if(key == KeyValues.RecordOnlyWhenForeground && value is bool b)
                    recordOnlyWhenForeground = b;
                if(key == KeyValues.MinPlayTimeRecordThreshold && value is int i)
                    _minPlayTimeRecordThreshold = i;
            }
        });
    }

    public override bool OnSearch(string key) =>
        Galgame is not null && string.Equals(Galgame.Uuid.ToString("D"), key, StringComparison.OrdinalIgnoreCase);

    public override string Title { get; } = "RecordPlayTimeTask_Title".GetLocalized();
}
