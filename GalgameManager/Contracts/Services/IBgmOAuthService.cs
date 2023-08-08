using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IBgmOAuthService
{
    Task StartOAuth();
    Task FinishOAuthWithUri(Uri uri);

    Task<bool> RefreshOAuthState();

    Task<string> GetOAuthStateString(bool forceRefresh=false);

    Task<BgmAccount> GetBgmAccountWithCache(bool forceRefresh=false);

    Task<bool> QuitLoginBgm();

    /// <summary>
    /// 使用刷新令牌刷新授权，如果还没到刷新时间则什么都不做
    /// </summary>
    Task TryRefreshOAuthAsync();
    
    public delegate void Delegate(BgmAccount bgmAccount);
    
    /// <summary>
    /// 当设置值改变时触发
    /// </summary>
    public event Delegate? OnOAuthStateChange;
    
    public static DateTime UnixTimeStampToDateTime( double unixTimeStamp )
    {
        return DateTime.UnixEpoch.AddSeconds( unixTimeStamp ).ToLocalTime();;
    }
}