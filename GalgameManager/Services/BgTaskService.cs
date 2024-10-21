using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class BgTaskService : IBgTaskService
{
    public event Action<BgTaskBase>? BgTaskAdded;
    public event Action<BgTaskBase>? BgTaskRemoved;
    
    private const string FileName = "bgTasks.json";
    
    private readonly List<BgTaskBase> _bgTasks = new();
    private readonly Dictionary<Type,string> _bgTasksString = new();
    private readonly IInfoService _infoService;

    public BgTaskService(IInfoService infoService)
    {
        _infoService = infoService;
        
        _bgTasksString[typeof(RecordPlayTimeTask)] = "-record";
        _bgTasksString[typeof(GetGalgameInSourceTask)] = "-getGalInSource";
        _bgTasksString[typeof(UnpackGameTask)] = "-unpack";
        _bgTasksString[typeof(SourceMoveTask)] = "-sourceMove";
    }
    
    public void SaveBgTasksString()
    {
        var result = string.Empty;
        foreach (BgTaskBase bgTask in _bgTasks)
        {
            //转换为json，再转换为base64（避免参数解析困难），再加上前缀
            if (_bgTasksString.TryGetValue(bgTask.GetType(), out var str))
                result += str + $" {JsonConvert.SerializeObject(bgTask).ToBase64()} ";
        }
        FileHelper.Save(FileName, result);
    }

    public async Task ResolvedBgTasksAsync()
    {
        var argStrings = FileHelper.Load<string>(FileName)?.Split() ?? Array.Empty<string>();
        for (var i = 0; i < argStrings.Length; i++)
        {
            if(argStrings[i].StartsWith("-") == false) continue;
            Type? bgTaskType = _bgTasksString.FirstOrDefault(x => x.Value == argStrings[i]).Key;
            if (bgTaskType == null) continue;
            if (JsonConvert.DeserializeObject(Utils.FromBase64(argStrings[++i]), bgTaskType) is not BgTaskBase bgTask)
                continue;
            await bgTask.RecoverFromJson();
            _ = AddTaskInternal(bgTask);
        }
        FileHelper.Delete(FileName);
    }

    public Task AddBgTask(BgTaskBase bgTask) => AddTaskInternal(bgTask);
    
    public IEnumerable<BgTaskBase> GetBgTasks() => _bgTasks;
   
    public T? GetBgTask<T>(string key) where T : BgTaskBase
    {
        return _bgTasks.FirstOrDefault(t => t is T && t.OnSearch(key)) as T;
    }

    private Task AddTaskInternal(BgTaskBase bgTask)
    {
        _bgTasks.Add(bgTask);
        try
        {
            Task t = bgTask.Run().ContinueWith(async previousTask =>
            {
                await Task.Delay(500);
                if (previousTask is { IsFaulted: true, Exception: not null })
                {
                    Exception e = previousTask.Exception;
                    if (previousTask.Exception.InnerExceptions.Count > 0)
                        e = previousTask.Exception.InnerExceptions[0];
                    _infoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
                        "BgTaskService_TaskFailed".GetLocalized(bgTask.Title), e);
                }
                else if (bgTask.CurrentProgress.NotifyWhenSuccess && bgTask.CurrentProgress.Current > 0) 
                    _infoService.Event(EventType.BgTaskSuccessEvent, InfoBarSeverity.Success,
                        "BgTaskService_TaskSuccess".GetLocalized(bgTask.Title), msg: bgTask.CurrentProgress.Message);
                if (!_bgTasks.Contains(bgTask)) return Task.CompletedTask;
                _bgTasks.Remove(bgTask);
                UiThreadInvokeHelper.Invoke(() => BgTaskRemoved?.Invoke(bgTask));
                return Task.CompletedTask;
            });
            UiThreadInvokeHelper.Invoke(()=>BgTaskAdded?.Invoke(bgTask));
            return t;
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
                "BgTaskService_TaskFailed".GetLocalized(bgTask.Title), e);
            _bgTasks.Remove(bgTask);
            return Task.CompletedTask;
        }
    }
}