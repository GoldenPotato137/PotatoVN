using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class BgTaskService : IBgTaskService
{
    public event Action<BgTaskBase>? BgTaskAdded;
    public event Action<BgTaskBase>? BgTaskRemoved;
    
    private const string FileName = "bgTasks.json";
    
    private readonly List<BgTaskBase> _bgTasks = new();
    private readonly Dictionary<Type,string> _bgTasksString = new();

    public BgTaskService()
    {
        _bgTasksString[typeof(RecordPlayTimeTask)] = "-record";
        _bgTasksString[typeof(GetGalgameInFolderTask)] = "-getGalInFolder";
        _bgTasksString[typeof(UnpackGameTask)] = "-unpack";
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
        Task t = bgTask.Run().ContinueWith(_ =>
        {
            _bgTasks.Remove(bgTask);
            UiThreadInvokeHelper.Invoke(() => BgTaskRemoved?.Invoke(bgTask));
        });
        UiThreadInvokeHelper.Invoke(()=>BgTaskAdded?.Invoke(bgTask));
        return t;
    }
}