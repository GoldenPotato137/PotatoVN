namespace GalgameManager.Enums;

/// <summary>
/// 指示身份验证类型的枚举
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// 无身份验证
    /// </summary>
    NoAuthentication,
    /// <summary>
    /// 使用 Windows Hello 身份验证
    /// </summary>
    WindowsHello,
    /// <summary>
    /// 使用自定义密码来进行身份验证
    /// </summary>
    CustomPassword
}
