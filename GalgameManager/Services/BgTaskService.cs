using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class BgTaskService : IBgTaskService
{
    public event Action<BgTaskBase>? BgTaskAdded;
    public event Action<BgTaskBase>? BgTaskRemoved;

    private const string FileName = "bgTasks.json";

    private readonly List<BgTaskBase> _bgTasks = new();
    private readonly object _bgTasksLock = new();
    private readonly Dictionary<Type,string> _bgTasksString = new();
    private readonly IInfoService _infoService;
    private readonly IFileService _fileService;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<JsonConverter> _converters = new();

    public BgTaskService(IInfoService infoService, IFileService fileService, IServiceProvider serviceProvider)
    {
        _infoService = infoService;
        _fileService = fileService;
        _serviceProvider = serviceProvider;

        RegisterBgTaskType(typeof(RecordPlayTimeTask), "-record");
        RegisterBgTaskType(typeof(GetGalgameInSourceTask), "-getGalInSource");
        RegisterBgTaskType(typeof(UnpackGameTask), "-unpack");
        RegisterBgTaskType(typeof(PackGameTask), "-pack");
        RegisterBgTaskType(typeof(SourceMoveTask), "-sourceMove");
        RegisterBgTaskType(typeof(GetGalgameCharactersFromRssTask), "-getGalChar");
        RegisterBgTaskType(typeof(DownloadCategoryImageTask), "-getCategoryImg");
        RegisterBgTaskType(typeof(CallMagpieTask), "-callMagpie");
        RegisterBgTaskType(typeof(GameMuteTask), "-gameMute");
        RegisterBgTaskType(typeof(KeyMappingTask), "-keyMap");
        RegisterBgTaskType(typeof(GameSaveDetectorTask), "-saveDetector");

        _converters.Add(new GalgameAndUidConverter());
        _converters.Add(new CategoryAndUuidConverter());
    }

    /// <summary>
    /// 注册某个BgTask类型对应的启动串前缀，使其可以随启动恢复（托盘重启恢复/测试场景）
    /// </summary>
    public void RegisterBgTaskType(Type type, string token) => _bgTasksString[type] = token;

    public void SaveBgTasksString()
    {
        var result = string.Empty;
        BgTaskBase[] snapshot;
        lock (_bgTasksLock)
        {
            snapshot = _bgTasks.ToArray();
        }

        foreach (BgTaskBase bgTask in snapshot)
        {
            //转换为json，再转换为base64（避免参数解析困难），再加上前缀
            if (_bgTasksString.TryGetValue(bgTask.GetType(), out var str))
                result += str + $" {JsonConvert.SerializeObject(bgTask, _converters.ToArray()).ToBase64()} ";
        }
        _fileService.Save(AppStoragePaths.LocalDataPath, FileName, result);
    }

    public async Task ResolvedBgTasksAsync()
    {
        var argStrings = _fileService.Read<string>(AppStoragePaths.LocalDataPath, FileName)?.Split() ?? Array.Empty<string>();
        for (var i = 0; i < argStrings.Length; i++)
        {
            if(argStrings[i].StartsWith("-") == false) continue;
            Type? bgTaskType = _bgTasksString.FirstOrDefault(x => x.Value == argStrings[i]).Key;
            if (bgTaskType == null) continue;
            BgTaskBase? bgTask = CreateBgTaskShell(bgTaskType, Utils.FromBase64(argStrings[++i]));
            if (bgTask is null) continue;
            await bgTask.RecoverFromJson();
            _ = AddTaskInternal(bgTask);
        }
        _fileService.Delete(AppStoragePaths.LocalDataPath, FileName);
    }

    /// 为恢复创建任务实例并填充序列化状态：有无参构造则直接创建（兼容原有可序列化类型），
    /// 否则经DI容器解析构造函数的服务依赖（如DownloadCategoryImageTask等DI化任务）
    private BgTaskBase? CreateBgTaskShell(Type bgTaskType, string json)
    {
        try
        {
            BgTaskBase bgTask = bgTaskType.GetConstructor(Type.EmptyTypes) is not null
                ? (BgTaskBase)Activator.CreateInstance(bgTaskType)!
                : (BgTaskBase)ActivatorUtilities.CreateInstance(_serviceProvider, bgTaskType);
            JsonConvert.PopulateObject(json, bgTask, new JsonSerializerSettings { Converters = _converters });
            return bgTask;
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: $"Failed to restore bg task {bgTaskType.Name}", e: e);
            return null;
        }
    }

    public T CreateBgTask<T>(params object[] args) where T : BgTaskBase
        => ActivatorUtilities.CreateInstance<T>(_serviceProvider, args);

    public Task AddBgTask(BgTaskBase bgTask) => AddTaskInternal(bgTask);

    public IEnumerable<BgTaskBase> GetBgTasks()
    {
        lock (_bgTasksLock)
        {
            return _bgTasks.ToArray();
        }
    }

    public T? GetBgTask<T>(string key) where T : BgTaskBase
    {
        BgTaskBase[] snapshot;
        lock (_bgTasksLock)
        {
            snapshot = _bgTasks.ToArray();
        }

        return snapshot.FirstOrDefault(t => t is T && t.OnSearch(key)) as T;
    }

    private Task AddTaskInternal(BgTaskBase bgTask)
    {
        try
        {
            string? rejectedDuplicateKey = null;
            lock (_bgTasksLock)
            {
                if (bgTask is IDeduplicatedBgTask { DeduplicationKey: { Length: > 0 } key } &&
                    _bgTasks.Any(existing => existing.GetType() == bgTask.GetType() &&
                                             existing is IDeduplicatedBgTask deduplicated &&
                                             string.Equals(deduplicated.DeduplicationKey, key,
                                                 StringComparison.Ordinal)))
                {
                    rejectedDuplicateKey = key;
                }
                else
                {
                    _bgTasks.Add(bgTask);
                }
            }

            if (rejectedDuplicateKey is not null)
            {
                _infoService.DeveloperEvent(
                    msg: $"Ignored duplicate background task: type={bgTask.GetType().Name}, " +
                         $"key={rejectedDuplicateKey}");
                return Task.CompletedTask;
            }

            if (bgTask.ProgressOnTrayIcon)
                bgTask.OnProgress += UpdateTrayIconLabel;

            UiThreadInvokeHelper.Invoke(() => BgTaskAdded?.Invoke(bgTask));

            Task runTask = bgTask.Run();

            return HandleBgTaskCompletionAsync(bgTask, runTask);
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
                "BgTaskService_TaskFailed".GetLocalized(bgTask.Title), e);
            TryRemoveBgTask(bgTask);
            return Task.CompletedTask;
        }
    }

    private async Task HandleBgTaskCompletionAsync(BgTaskBase bgTask, Task runTask)
    {
        Exception? exception = null;
        try
        {
            try
            {
                await runTask.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                exception = e;
            }

            await Task.Delay(500).ConfigureAwait(false);

            if (exception is not null)
            {
                _infoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
                    "BgTaskService_TaskFailed".GetLocalized(bgTask.Title), exception);
            }
            else if (bgTask.CurrentProgress.NotifyWhenSuccess && bgTask.CurrentProgress.Current > 0)
            {
                _infoService.Event(EventType.BgTaskSuccessEvent, InfoBarSeverity.Success,
                    "BgTaskService_TaskSuccess".GetLocalized(bgTask.Title), msg: bgTask.CurrentProgress.Message,
                    callbackAction: bgTask.EventAction, callbackButtonText: bgTask.EventActionText);
            }
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: "HandleBgTaskCompletionAsync Failed", e: e);
        }
        finally
        {
            var removed = TryRemoveBgTask(bgTask);
            if (removed)
            {
                UiThreadInvokeHelper.Invoke(() => BgTaskRemoved?.Invoke(bgTask));
            }

            if (bgTask.ProgressOnTrayIcon)
            {
                bgTask.OnProgress -= UpdateTrayIconLabel;
                UpdateTrayIconLabel(new Progress());
            }
        }
    }

    private bool TryRemoveBgTask(BgTaskBase bgTask)
    {
        lock (_bgTasksLock)
        {
            return _bgTasks.Remove(bgTask);
        }
    }

    private void UpdateTrayIconLabel(Progress _)
    {
        try
        {
            BgTaskBase[] snapshot;
            lock (_bgTasksLock)
            {
                snapshot = _bgTasks.ToArray();
            }

            UiThreadInvokeHelper.Invoke(() =>
            {
                try
                {
                    var label = $"{"AppDisplayName".GetLocalized()}\n";
                    foreach (BgTaskBase bgTask in snapshot)
                    {
                        if (bgTask.ProgressOnTrayIcon)
                            label += $"{bgTask.CurrentProgress.Message}\n";
                    }

                    label = label.TrimEnd('\n'); //去掉最后一个换行符
                    if (App.SystemTray is not null)
                        App.SystemTray.ToolTipText = label;
                }
                catch (Exception e) // 更新托盘这个事情就算挂了也不影响整个软件的运行，所以这里不需要抛出异常炸掉整个软件
                {
                    _infoService.DeveloperEvent(msg: "Update TrayIcon Label Failed", e: e);
                }
            });
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: "Update TrayIcon Label Failed", e: e); // 不太可能发生，为了保险起见还是加上
        }
    }
}
