using GalgameManager.Helpers;

namespace GalgameManager.Models;

public class BgmAccount
{
    public bool OAuthed =>  BangumiAccessToken is not "";
    public DateTime Expires = DateTime.Now;
    public DateTime NextRefresh = DateTime.MinValue;
    public string UserId = "";
    public string BangumiAccessToken = "";
    public string BangumiRefreshToken = "";
    /// 用户头像路径
    public string Avatar = string.Empty;
    /// 用户名
    public string Name = "BgmAccount_NoName".GetLocalized();
}