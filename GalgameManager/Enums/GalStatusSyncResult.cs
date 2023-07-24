namespace GalgameManager.Enums;

public enum GalStatusSyncResult
{
    /// OK
    Ok,
    /// 没有权限（可能是没有登录）
    UnAuthorized,
    /// 当前galgame没有被解析
    NoId,
    /// 其他错误
    Other
}