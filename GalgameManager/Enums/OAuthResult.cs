using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Enums;

public enum OAuthResult
{
    ///成功
    Done,
    ///失败
    Failed,
    ///获取Token中
    FetchingToken,
    ///获取账户信息中
    FetchingAccount,
}

public static class OAuthResultHelper
{
    /// <summary>
    /// 转换到InfoBarSeverity，用于显示提示信息
    /// </summary>
    public static InfoBarSeverity ToInfoBarSeverity(this OAuthResult result)
    {
        switch (result)
        {
            case OAuthResult.Done:
                return InfoBarSeverity.Success;
            case OAuthResult.Failed:
                return InfoBarSeverity.Error;
            case OAuthResult.FetchingAccount:
            case OAuthResult.FetchingToken:
            default:
                return InfoBarSeverity.Informational;
        }
    }

    public static string ToMsg(this OAuthResult result)
    {
        return (nameof(OAuthResult) + "_" + result).GetLocalized();
    }
}