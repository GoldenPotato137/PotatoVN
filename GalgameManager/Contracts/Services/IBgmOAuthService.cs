using GalgameManager.Enums;

namespace GalgameManager.Contracts.Services;

public interface IBgmOAuthService
{
    /// <summary>
    /// 检查账户是否可用，检查是否需要刷新授权
    /// </summary>
    /// <returns></returns>
    Task Init();
    
    Task StartOAuthAsync();
    Task FinishOAuthWithUri(Uri uri);

    Task AuthWithAccessToken(string accessToken);

    /// <summary>
    /// 使用更新token获取新的token，并刷新缓存 <br/>
    /// 如果没有refreshToken则什么都不做
    /// </summary>
    /// <returns>是否成功</returns>
    Task<bool> RefreshAccountAsync();

    Task LogoutAsync();

    /// <summary>
    /// 当授权状态改变时触发（用于提示当前授权获取进度）
    /// </summary>
    public event Action<BgmOAuthStatus> OnAuthResultChange; 

    public static DateTime UnixTimeStampToDateTime( double unixTimeStamp )
    {
        return DateTime.UnixEpoch.AddSeconds( unixTimeStamp ).ToLocalTime();;
    }
}