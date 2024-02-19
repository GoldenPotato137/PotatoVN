using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IBangumiService
{
    public bool IsOauth2Enable { get; }
    public bool IsLoginEnable { get; }
    
    /// <summary>
    /// 使用code完成授权，需要在外部捕获异常
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public Task<BangumiToken> GetTokenWithCodeAsync(string code);
    
    /// <summary>
    /// 使用refresh token完成授权，需要在外部捕获异常
    /// </summary>
    public Task<BangumiToken> GetTokenWithRefreshTokenAsync(string refreshToken);
    
    /// <summary>
    /// 使用token获取Token信息，需要在外部捕获异常
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<BangumiToken> GetTokenWithTokenAsync(string token);

    /// <summary>
    /// 使用token获取账户信息，需要在外部捕获异常
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<BangumiAccount> GetAccount(string token);
}