using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Services;

public partial class GalgameCollectionService
{
    public async Task SyncUpgrade()
    {
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.GameSyncUpgraded) == false &&
            await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGames))
        {
            var syncDbFile = GetSyncDbFile();
        }
    }

    /// <summary>
    /// 获取同步数据库文件，如果没有设置同步文件夹，或者没有Mac地址，返回空字符串
    /// </summary>
    private string GetSyncDbFile()
    {
        var remotePath = LocalSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder).Result;
        var mac = Utils.GetMacAddress();
        if (string.IsNullOrEmpty(remotePath) == false && string.IsNullOrEmpty(mac) == false)
            return Path.Combine(remotePath, "PotatoVN", $"{mac}.db");
        return string.Empty;
    }
}