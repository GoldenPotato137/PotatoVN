using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class AutoExportService : IAutoExportService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly HashSet<string> RelevantSettingKeys =
    [
        KeyValues.AutoExport,
        KeyValues.AutoExportInterval,
        KeyValues.AutoExportPath,
        KeyValues.LastExportTime,
        KeyValues.MaxBackupNumber,
    ];

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IInfoService _infoService;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly SemaphoreSlim _exportLock = new(1, 1);
    private readonly object _lifecycleLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _runTask;

    public AutoExportService(ILocalSettingsService localSettingsService, IBgTaskService bgTaskService,
        IInfoService infoService, TimeProvider timeProvider)
    {
        _localSettingsService = localSettingsService;
        _bgTaskService = bgTaskService;
        _infoService = infoService;
        _timeProvider = timeProvider;
        _localSettingsService.OnSettingChanged += OnSettingChanged;
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_runTask is { IsCompleted: false }) return;
            _cancellationTokenSource = new CancellationTokenSource();
            _runTask = RunAsync(_cancellationTokenSource.Token);
        }
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            _cancellationTokenSource?.Cancel();
        }
        WakeUp();
    }

    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            string? path = await _localSettingsService.ReadSettingAsync<string>(KeyValues.AutoExportPath);
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                await _localSettingsService.SaveSettingAsync(KeyValues.AutoExport, false);
                return false;
            }
        }

        await _localSettingsService.SaveSettingAsync(KeyValues.AutoExport, enabled);
        return true;
    }

    public Task<bool> ExportAsync(string targetPath) =>
        ExportInternalAsync(targetPath, pruneOldBackups: false, CancellationToken.None);

    protected virtual BgTaskBase CreateExportTask(string targetPath) => new ExportTask(targetPath);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // 失败后本次运行内不再自动导出，避免持续失败时反复弹通知；下次启动软件时重新开始调度
            try
            {
                if (!await CheckAndExportAsync(cancellationToken))
                {
                    NotifyPaused();
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                NotifyPaused(e);
                return;
            }

            try
            {
                await WaitForNextCheckAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <returns>自动导出失败时返回 false；未到期、无需导出或导出成功时返回 true。</returns>
    private async Task<bool> CheckAndExportAsync(CancellationToken cancellationToken)
    {
        if (!await _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoExport)) return true;

        DateTime lastExportTime = await _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastExportTime);
        double intervalHours = await _localSettingsService.ReadSettingAsync<double>(KeyValues.AutoExportInterval);
        TimeSpan interval = double.IsFinite(intervalHours) && intervalHours > 0
            ? TimeSpan.FromHours(intervalHours)
            : TimeSpan.FromHours(1);
        if (Now < lastExportTime.Add(interval)) return true;

        string? path = await _localSettingsService.ReadSettingAsync<string>(KeyValues.AutoExportPath);
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return true;

        bool started = await ExportInternalAsync(path, pruneOldBackups: true, cancellationToken);
        if (!started) return true;

        // ExportTask 的异常由 BgTaskService 捕获并通知，这里只能通过导出时间是否更新来判断成败
        DateTime updatedExportTime = await _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastExportTime);
        return updatedExportTime > lastExportTime;
    }

    private void NotifyPaused(Exception? exception = null) =>
        _infoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
            "AutoExportService_Paused".GetLocalized(), exception);

    private async Task<bool> ExportInternalAsync(string targetPath, bool pruneOldBackups,
        CancellationToken cancellationToken)
    {
        if (!await _exportLock.WaitAsync(0, cancellationToken)) return false;
        try
        {
            if (_bgTaskService.GetBgTask<ExportTask>(string.Empty) is not null) return false;
            if (pruneOldBackups) await PruneOldBackupsAsync(targetPath);
            await _bgTaskService.AddBgTask(CreateExportTask(targetPath));
            return true;
        }
        finally
        {
            _exportLock.Release();
        }
    }

    private async Task PruneOldBackupsAsync(string path)
    {
        int maxBackupNumber = await _localSettingsService.ReadSettingAsync<int?>(KeyValues.MaxBackupNumber) ?? 999;
        maxBackupNumber = Math.Max(maxBackupNumber, 1);
        List<string> files = Directory.GetFiles(path, "*.pvnExport.zip")
            .OrderBy(File.GetCreationTime)
            .ToList();
        int deleteCount = files.Count - maxBackupNumber + 1;
        for (var i = 0; i < deleteCount; i++)
        {
            try
            {
                File.Delete(files[i]);
            }
            catch (Exception e)
            {
                _infoService.DeveloperEvent(e: e);
            }
        }
    }

    private async Task WaitForNextCheckAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource waitCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task delayTask = Task.Delay(CheckInterval, _timeProvider, waitCancellationTokenSource.Token);
        Task signalTask = _wakeSignal.WaitAsync(waitCancellationTokenSource.Token);
        Task completedTask = await Task.WhenAny(delayTask, signalTask);
        await waitCancellationTokenSource.CancelAsync();
        await completedTask;
    }

    private void OnSettingChanged(string key, object? value)
    {
        if (RelevantSettingKeys.Contains(key)) WakeUp();
    }

    private void WakeUp()
    {
        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // 已有一个待处理的唤醒信号时无需重复排队。
        }
    }

    private DateTime Now => _timeProvider.GetLocalNow().DateTime;
}
