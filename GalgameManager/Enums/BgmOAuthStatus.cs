namespace GalgameManager.Enums;

public enum BgmOAuthStatus
{
    ///成功
    Done,
    ///失败
    Failed,
    ///获取Token中
    FetchingToken,
    ///获取账户信息中
    FetchingAccount,
    /// 获取token信息中
    FetchingTokenInfo,
    /// 获取账户头像中
    FetchingAccountImage,
}