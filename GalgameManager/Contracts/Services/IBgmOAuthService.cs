namespace GalgameManager.Contracts.Services;

public class BgmOAuthState
{
    public bool OAuthed;
    public int Expires;
    public string UserId;

    public BgmOAuthState(bool oAuthed, int expires, string userId)
    {
        OAuthed = oAuthed;
        Expires = expires;
        UserId = userId;
    }
    public BgmOAuthState()
    {
        OAuthed = false;
        Expires = 0;
        UserId = "";
    }
}

public interface IBgmOAuthService
{
    Task StartOAuth();
    Task<BgmOAuthState> FinishOAuthWithUri(string uri);
    Task<BgmOAuthState> FinishOAuthWithCode(string code);
    Task<BgmOAuthState> CheckOAuthState();

    Task<BgmOAuthState> RefreshOAuthState();
    
    public delegate void Delegate(BgmOAuthState bgmOAuthState);
    
    /// <summary>
    /// 当设置值改变时触发
    /// </summary>
    public event Delegate? OnOAuthStateChange;
}