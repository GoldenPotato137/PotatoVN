namespace GalgameManager.Contracts.Services;

public interface IAuthenticationService
{
    /// <summary>
    /// 开始进行身份验证
    /// </summary>
    /// <returns>指示身份验证是否成功的值</returns>
    Task<bool> StartAuthentication();
}
