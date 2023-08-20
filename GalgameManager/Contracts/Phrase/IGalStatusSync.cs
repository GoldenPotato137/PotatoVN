using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

/// <summary>
/// 与信息源同步游玩状态的接口
/// </summary>
public interface IGalStatusSync
{
    /// <summary>
    /// 上传游玩状态
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <returns>(结果， 结果解释信息)</returns>
    public Task<(GalStatusSyncResult, string)> UploadAsync(Galgame galgame);
    
    /// <summary>
    /// 下载游玩状态
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <returns>(结果， 结果解释信息)</returns>
    public Task<(GalStatusSyncResult, string)> DownloadAsync(Galgame galgame);
    
    /// <summary>
    /// 下载玩家在信息源上所有游戏的游玩状态
    /// </summary>
    /// <param name="galgames">游戏列表</param>
    /// <returns>(结果，结果解释信息)</returns>
    public Task<(GalStatusSyncResult, string)> DownloadAllAsync(List<Galgame> galgames);
}