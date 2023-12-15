using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Newtonsoft.Json;
using SQLite;

namespace GalgameManager.Services;

public partial class GalgameCollectionService
{
    /// 同步状态改变事件， 当前进度/总commit数
    public event GenericDelegate<(int, int)>? SyncProgressChanged;
    private bool _readyForSync;
    private SQLiteAsyncConnection? _localDb;
    
    public async Task SyncUpgrade()
    {
        _readyForSync = await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.GameSyncUpgraded) && 
                        await Utils.CheckInternetConnection();
        
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.GameSyncUpgraded) == false &&
            await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGames))
        {
            SQLiteAsyncConnection? conn = GetConnection();
            if (conn is null) return;
            await conn.CreateTableAsync<SyncCommit>();
            List<SyncCommit> oldTable = await conn.Table<SyncCommit>().ToListAsync();
            foreach (Galgame galgame in _galgames)
            {
                if(string.IsNullOrEmpty(galgame.Ids[(int)RssType.Bangumi])) continue;
                if (oldTable.Any(commit => commit.Type == CommitType.Add && commit.BgmId == galgame.Ids[(int)RssType.Bangumi]))
                    continue; //防止重装软件后重复添加
                await conn.InsertAsync(new SyncCommit(CommitType.Add,galgame.Ids[(int)RssType.Bangumi]!, 
                    new AddCommit
                {
                    Name = galgame.Name.Value ?? string.Empty
                }));
                foreach (var date in galgame.PlayedTime.Keys)
                {
                    await conn.InsertAsync(new SyncCommit(CommitType.Play,galgame.Ids[(int)RssType.Bangumi]!, 
                        new PlayCommit
                    {
                        Date = date,
                        Time = galgame.PlayedTime[date]
                    }));
                }
            }
            _readyForSync = true;
            await conn.CloseAsync();
            await LocalSettingsService.SaveSettingAsync(KeyValues.GameSyncUpgraded, true);
        }
    }

    /// <summary>
    /// 从同步盘中同步游戏数据，如果设置中没有打开同步游戏则什么都不做
    /// </summary>
    public async Task SyncGames()
    {
        if (_readyForSync == false) return;
        
        List<string> dbFiles = GetSyncDbFiles();
        dbFiles.Remove(GetSyncDbFile());
        Dictionary<string, int> syncTo = await LocalSettingsService.ReadSettingAsync<Dictionary<string, int>>(KeyValues.SyncTo) 
                                         ?? new Dictionary<string, int>();
        
        List<SyncCommit> commits = new();
        Dictionary<SyncCommit, string> commitMacMap = new();
        foreach (var dbFile in dbFiles)
        {
            SQLiteAsyncConnection conn = new(dbFile);
            var mac = new FileInfo(dbFile).Name[..^3]; //去掉.db
            syncTo.TryGetValue(mac, out var lastId);
            List<SyncCommit> commit = await conn.Table<SyncCommit>().Where(c => c.Id > lastId).ToListAsync();
            foreach (SyncCommit c in commit)
                commitMacMap[c] = mac;
            commits.AddRange(commit);
            if(commit.Count > 0)
                syncTo[mac] = commit.Max(c => c.Id);
            await conn.CloseAsync();
        }
        commits.Sort((a, b) =>
        {
            if (a.Timestamp != b.Timestamp) return a.Timestamp.CompareTo(b.Timestamp);
            if (a.BgmId != b.BgmId) return string.Compare(a.BgmId, b.BgmId, StringComparison.Ordinal);
            if(a.Type == b.Type) return a.Id.CompareTo(b.Id);
            return a.Type.CompareTo(b.Type);
        });

        var syncCnt = 0;
        foreach (SyncCommit commit in commits)
        {
            SyncProgressChanged?.Invoke((syncCnt++, commits.Count));
            Galgame? game = GetGalgameFromId(commit.BgmId, RssType.Bangumi);
            if(game is not null && game.SyncTo.TryGetValue(commitMacMap[commit], out var lastId) && commit.Id <= lastId) 
                continue;
            try
            {
                switch (commit.Type)
                {
                    case CommitType.Add:
                        if (game is not null) continue;
                        if (commits.Any(c => c.Timestamp >= commit.Timestamp && 
                                             c.Type == CommitType.Delete && c.Id == commit.Id)) continue;
                        AddCommit? addCommit = JsonConvert.DeserializeObject<AddCommit>(commit.Content);
                        if (addCommit is null) continue;
                        game = await TryAddGalgameAsync(addCommit, commit.BgmId);
                        break;
                    case CommitType.Play:
                        if (game is null) continue;
                        PlayCommit? playCommit = JsonConvert.DeserializeObject<PlayCommit>(commit.Content);
                        if (playCommit is null) continue;
                        game.PlayedTime.TryGetValue(playCommit.Date, out var time);
                        game.PlayedTime[playCommit.Date] = time + playCommit.Time;
                        game.TotalPlayTime += playCommit.Time;
                        if (Galgame.GetTime(playCommit.Date) >
                            Galgame.GetTime(game.LastPlay.Value ?? Galgame.DefaultString))
                        {
                            game.LastPlay = playCommit.Date;
                            UpdateDisplay(UpdateType.Play, game);
                        }
                        break;
                    case CommitType.Delete:
                        if (game is null) continue;
                        await RemoveGalgame(game, false);
                        break;
                    case CommitType.ChangePlayType:
                        break;
                }

                if (game is not null)
                {
                    game.SyncTo[commitMacMap[commit]] = commit.Id;
                    await SaveGalgamesAsync(game); //在这里保存是为了防止同步到一半软件关闭导致同步到的id错误
                }
            }
            catch
            {
                //ignore
            }
        }

        await LocalSettingsService.SaveSettingAsync(KeyValues.SyncTo, syncTo);
        SyncProgressChanged?.Invoke((commits.Count, commits.Count));

        _localDb = GetConnection();
        if(_localDb is not null)
            await _localDb.CreateTableAsync<SyncCommit>();

        async void OnAppClosing()
        {
            if (_localDb is not null) await _localDb.CloseAsync();
        }

        App.OnAppClosing += OnAppClosing;
    }

    /// <summary>
    /// 提交同步更改，如果设置中没有打开同步游戏则什么都不做
    /// </summary>
    /// <param name="type">更改类型</param>
    /// <param name="galgame">更改游戏</param>
    /// <param name="content">内容<br/>
    /// 对于play (string, int) => (日期, 时间)<br/>
    /// </param>
    public async Task CommitChange(CommitType type, Galgame galgame, object? content)
    {
        if (_readyForSync == false || _localDb is null) return;
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Bangumi])) return;
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGames) == false) return;
        switch (type)
        {
            case CommitType.Add:
                await _localDb.InsertAsync(new SyncCommit(CommitType.Add, galgame.Ids[(int)RssType.Bangumi]!, new AddCommit
                {
                    Name = galgame.Name.Value ?? string.Empty
                }));
                break;
            case CommitType.Delete:
                await _localDb.InsertAsync(new SyncCommit(CommitType.Delete, galgame.Ids[(int)RssType.Bangumi]!, new DeleteCommit()));
                break;
            case CommitType.Play:
                if (content is not (string date, int time)) return;
                await _localDb.InsertAsync(new SyncCommit(CommitType.Play, galgame.Ids[(int)RssType.Bangumi]!, new PlayCommit
                {
                    Date = date,
                    Time = time
                }));
                break;
            case CommitType.ChangePlayType:
                break;
        }
    }

    /// <summary>
    /// 获取数据库链接，若文件不存在则会创建，如果没有设置同步文件夹，或者没有Mac地址，返回null
    /// </summary>
    /// <returns></returns>
    private SQLiteAsyncConnection? GetConnection()
    {
        var syncDbFile = GetSyncDbFile();
        if (string.IsNullOrEmpty(syncDbFile)) return null;
        FileInfo file = new(syncDbFile);
        if (file.Directory?.Exists == false)
            file.Directory.Create();
        return new SQLiteAsyncConnection(syncDbFile);
    }

    /// <summary>
    /// 获取同步数据库文件，如果没有设置同步文件夹，或者没有Mac地址，返回空字符串
    /// </summary>
    private string GetSyncDbFile()
    {
        var remotePath = LocalSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder).Result;
        var mac = Utils.GetMacAddress();
        if (string.IsNullOrEmpty(remotePath) == false && string.IsNullOrEmpty(mac) == false)
        {
            return Path.Combine(remotePath, "PotatoVN", $"{mac}.db");
        }
        return string.Empty;
    }

    /// <summary>
    /// 获取同步数据库文件列表（包括本机）
    /// </summary>
    private List<string> GetSyncDbFiles()
    {
        var remotePath = LocalSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder).Result;
        if (string.IsNullOrEmpty(remotePath) == false)
        {
            remotePath = Path.Combine(remotePath, "PotatoVN");
            return new List<string>(Directory.GetFiles(remotePath, "*.db", SearchOption.AllDirectories));
        }
        return new List<string>();
    }
}