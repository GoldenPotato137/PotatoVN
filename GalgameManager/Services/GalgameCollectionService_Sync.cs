using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Newtonsoft.Json;
using SQLite;

namespace GalgameManager.Services;

public partial class GalgameCollectionService
{
    private bool _readyForSync;
    
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
            foreach (Galgame galgame in _galgames)
            {
                if(string.IsNullOrEmpty(galgame.Ids[(int)RssType.Bangumi])) continue;
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
            // await LocalSettingsService.SaveSettingAsync(KeyValues.GameSyncUpgraded, true); //todo:TEST ONLY
        }
    }

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
            syncTo[mac] = commit.Max(c => c.Id);
        }
        commits.Sort((a, b) =>
        {
            if (a.Timestamp == b.Timestamp)
            {
                if (a.BgmId == b.BgmId)
                {
                    if(a.Type == b.Type)
                        return a.Id.CompareTo(b.Id);
                    return a.Type.CompareTo(b.Type);
                }
                return string.Compare(a.BgmId, b.BgmId, StringComparison.Ordinal);
            }
            return a.Timestamp.CompareTo(b.Timestamp);
        });

        foreach (SyncCommit commit in commits)
        {
            Galgame? game = GetGalgameFromId(commit.BgmId, RssType.Bangumi);
            if(game is not null && game.SyncTo.TryGetValue(commitMacMap[commit], out var lastId) && commit.Id <= lastId) 
                continue;
            try
            {
                switch (commit.Type)
                {
                    case CommitType.Add:
                        if (game is not null) continue;
                        if (commits.Any(c => c.Type == CommitType.Delete && c.Id == commit.Id)) continue;
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
                        break;
                    case CommitType.Delete:
                        break;
                    case CommitType.ChangePlayType:
                        break;
                }
                
                if(game is not null)
                    game.SyncTo[commitMacMap[commit]] = commit.Id;
            }
            catch
            {
                //ignore
            }
        }

        // await LocalSettingsService.SaveSettingAsync(KeyValues.SyncTo, syncTo); //todo: TEST ONLY
        await SaveGalgamesAsync();
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