using System.Collections.Concurrent;
using GalgameManager.Contracts.BgTasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.API;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameCharactersFromRssTask : BgTaskBase, IGameProcessQueue
{
    public ConcurrentQueue<Galgame?> GetCharactersQueue = new();
    public int MaxRunning = 3;
    private readonly List<Task> _runningTasks = new();
    private readonly object _runningTasksLock = new();
    private readonly ConcurrentDictionary<Galgame, string> _runningTasksMsg = new();
    private readonly object _changeMsgLock = new();
    private readonly ILocalSettingsService _settings;
    private readonly IPvnService _pvnService;
    private readonly IGalgameCollectionService _gameService;

    public GetGalgameCharactersFromRssTask(ILocalSettingsService settings, IPvnService pvnService,
        IGalgameCollectionService gameService)
    {
        _settings = settings;
        _pvnService = pvnService;
        _gameService = gameService;
    }
    
    /// 添加一个galgame到解析队列中
    public void AddGalgame(Galgame? game)
    {
        if (game is null) return;
        GetCharactersQueue.Enqueue(game);
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal()
    {
        return Task.Run((async Task () =>
        {
            while (true)
            {
                lock (_runningTasksLock)
                {
                    if (GetCharactersQueue.IsEmpty && _runningTasks.Count == 0) break;
                    _runningTasks.RemoveAll(t => t.IsCompleted);
                    while (_runningTasks.Count < MaxRunning && GetCharactersQueue.TryDequeue(out Galgame? game))
                    {
                        Task t = GetCharacterAsync(game, progress =>
                        {
                            if (game is null) return;
                            lock (_changeMsgLock)
                                _runningTasksMsg[game] = $"{progress.Message}, {progress.Current}/{progress.Total}";
                            UpdateProgressMsg();
                        }).ContinueWith(t =>
                        {
                            if (game is null) return;
                            lock (_changeMsgLock) // 防止这个时候正在迭代更新msg，导致迭代过程中容器被修改
                            {
                                _runningTasksMsg.TryRemove(game, out _);
                            }
                        });
                        _runningTasks.Add(t);
                    }
                }
                UpdateProgressMsg();
                await Task.Delay(500);
            }
        })!);

        void UpdateProgressMsg()
        {
            lock (_changeMsgLock)
            {
                int runningTasksCount;
                lock (_runningTasksLock)
                {
                    runningTasksCount = _runningTasks.Count;
                }
                var msg = string.Empty;
                foreach (var (game, message) in _runningTasksMsg)
                    msg += $"{game.Name.Value}: {message}\n";
                msg += "Galgame_GetCharacterInfo_RunningTasks".GetLocalized(GetCharactersQueue.Count);
                ChangeProgress(runningTasksCount, runningTasksCount + GetCharactersQueue.Count, msg);
            }
        }
    }

    /// 获取角色信息，注意捕获异常，若game为null则什么都不做
    private async Task GetCharacterAsync(Galgame? game, Action<Progress> onProgress)
    {
        if (game is null) return;
        var log = string.Empty;
        log += $"{DateTime.Now}\n{game.Name.Value}\n\n";
        var total = game.Characters.Count;
        var galgameName = game.Name.Value ?? string.Empty;
        for (var i = 0; i < game.Characters.Count; i++)
        {
            GalgameCharacter character = game.Characters[i];
            onProgress.Invoke(new Progress
            {
                Current = i, Total = total,
                Message = "Galgame_GetCharacterInfo_GettingInfo".GetLocalized(character.Name),
            });
            await UiThreadInvokeHelper.InvokeAsync(async Task () =>
            {
                for (var retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        character = await _gameService.PhraseGalCharacterAsync(character, game.RssType, game.Uuid);
                        break;
                    }
                    catch (ThrottledException)
                    {
                        //等待1分钟
                        for (var wait = 60; wait > 0; wait -= 10)
                        {
                            onProgress.Invoke(new Progress
                            {
                                Current = i, Total = total,
                                Message = "Galgame_GetCharacterInfo_Waiting".GetLocalized(wait),
                            });
                            await Task.Delay(1000 * 10);
                        }
                    }
                }
            });
            log += $"{game.Name.Value}->{character.Name} Done\n";
            await _gameService.SaveGalgameAsync(game);
        }
        onProgress.Invoke(new Progress
        {
            Current = total, Total = total,
            Message = "Galgame_GetCharacterInfo_Saving".GetLocalized(),
        });
        await _gameService.SaveGalgameAsync(game);
        if (await _settings.ReadSettingAsync<bool>(KeyValues.SyncGames))
            _pvnService.Upload(game, PvnUploadProperties.Character);
        
        FileHelper.SaveWithoutJson(game.GetLogName(), log, "Logs");
        await Task.Delay(1000); //等待文件保存
        onProgress.Invoke(new Progress
        {
            Current = 1, Total = 1,
            Message = "Galgame_GetCharacterInfo_Done".GetLocalized(galgameName),
        });
    }

    public override bool OnSearch(string key) => true;

    public override string Title { get; } = "GetCharacterInfoTask_Title".GetLocalized();
}
