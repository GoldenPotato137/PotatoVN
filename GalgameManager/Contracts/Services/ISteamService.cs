using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface ISteamService
{
    public Uri BaseUri { get; }

    /// <summary>
    /// Steam账号的信息
    /// 得到这个服务需要用户的steamid
    /// </summary>
    public SteamAccount? GetSteamAccountInfo(string steamid);

    /// <summary>
    /// 这个是得到steam所有的游戏列表
    /// 要求是用户必须公开
    /// /// </summary>
    public Dictionary<string,SteamGame> GetSteamGameList(string steamid);

    /// <summary>
    /// 这里我们更新游戏数据
    /// </summary>
    public void UpdateSteamGameInfo();

}
