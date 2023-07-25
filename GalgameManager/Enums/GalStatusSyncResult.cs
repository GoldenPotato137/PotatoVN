using Microsoft.UI.Xaml.Controls;

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
    Other,
    /// 该信息源不支持同步
    NotSupported
}

public static class GalStatusSyncResultHelper
{
    /// <summary>
    /// 转换到InfoBarSeverity，用于显示提示信息
    /// </summary>
    public static InfoBarSeverity ToInfoBarSeverity(this GalStatusSyncResult result)
    {
        switch (result)
        {
            case GalStatusSyncResult.Ok:
                return InfoBarSeverity.Success;
            case GalStatusSyncResult.UnAuthorized:
            case GalStatusSyncResult.NoId:
            case GalStatusSyncResult.Other:
                return InfoBarSeverity.Error;
            case GalStatusSyncResult.NotSupported:
                return InfoBarSeverity.Warning;
            default:
                return InfoBarSeverity.Error;
        }
    }
}