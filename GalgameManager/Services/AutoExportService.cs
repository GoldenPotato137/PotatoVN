using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models.BgTasks;

namespace GalgameManager.Services;

public class AutoExportService : IAutoExportService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailureRetryInterval = TimeSpan.FromMinutes(5);
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
    private DateTime _retryNotBefore = DateTime.MinValue;

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
            try
            {
                await CheckAndExportAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                _retryNotBefore = Now.Add(FailureRetryInterval);
                _infoService.DeveloperEvent(e: e);
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

    private async Task CheckAndExportAsync(CancellationToken cancellationToken)
    {
        if (!await _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoExport))
        {
            _retryNotBefore = DateTime.MinValue;
            return;
        }

        DateTime now = Now;
        if (now < _retryNotBefore) return;

        DateTime lastExportTime = await _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastExportTime);
        double intervalHours = await _localSettingsService.ReadSettingAsync<double>(KeyValues.AutoExportInterval);
        TimeSpan interval = double.IsFinite(intervalHours) && intervalHours > 0
            ? TimeSpan.FromHours(intervalHours)
            : TimeSpan.FromHours(1);
        if (now < lastExportTime.Add(interval)) return;

        string? path = await _localSettingsService.ReadSettingAsync<string>(KeyValues.AutoExportPath);
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        bool started = await ExportInternalAsync(path, pruneOldBackups: true, cancellationToken);
        if (!started) return;

        DateTime updatedExportTime = await _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastExportTime);
        _retryNotBefore = updatedExportTime > lastExportTime
            ? DateTime.MinValue
            : now.Add(FailureRetryInterval);
    }

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
