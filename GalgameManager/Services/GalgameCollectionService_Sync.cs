using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
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
                await conn.InsertAsync(new SyncCommit(CommitType.Add, new AddCommit
                {
                    bgmId = galgame.Ids[(int)RssType.Bangumi]!,
                    name = galgame.Name.Value ?? string.Empty
                }));
            }
            _readyForSync = true;
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
}