namespace GalgameManager.Contracts.Services;

public class BgmOAuthState
{
    public bool OAuthed =>  BangumiAccessToken is not "";
    public DateTime Expires = DateTime.Now;
    public string UserId = "";
    public string BangumiAccessToken = "";
    public string BangumiRefreshToken = "";
}

public interface IBgmOAuthService
{
    Task StartOAuth();
    Task FinishOAuthWithUri(Uri uri);

    Task<bool> RefreshOAuthState();

    Task<string> GetOAuthStateString(bool forceRefresh=false);

    Task<BgmOAuthState?> GetOAuthState(bool forceRefresh=false);

    Task<bool> QuitLoginBgm();
    
    public delegate void Delegate(BgmOAuthState bgmOAuthState);
    
    /// <summary>
    /// 当设置值改变时触发
    /// </summary>
    public event Delegate? OnOAuthStateChange;
    
    public static DateTime UnixTimeStampToDateTime( double unixTimeStamp )
    {
        return DateTime.UnixEpoch.AddSeconds( unixTimeStamp ).ToLocalTime();;
    }
}