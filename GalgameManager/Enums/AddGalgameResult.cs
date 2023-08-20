using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Enums;

public enum AddGalgameResult
{
    Success,
    AlreadyExists,
    NotFoundInRss,
    Other
}

public static class AddGalgameResultHelper
{
    public static string ToMsg(this AddGalgameResult result)
    {
        switch (result)
        {
            case AddGalgameResult.Success:
                return "AddGalgameResult_Success".GetLocalized();
            case AddGalgameResult.AlreadyExists:
                return "AddGalgameResult_AlreadyInLibrary".GetLocalized();
            case AddGalgameResult.NotFoundInRss:
                return "AddGalgameResult_NotFoundInRss".GetLocalized();
        }

        return string.Empty;
    }
    
    public static InfoBarSeverity ToInfoBarSeverity(this AddGalgameResult result)
    {
        switch (result)
        {
            case AddGalgameResult.Success:
                return InfoBarSeverity.Success;
            case AddGalgameResult.NotFoundInRss:
                return InfoBarSeverity.Warning;
            case AddGalgameResult.Other:
            case AddGalgameResult.AlreadyExists:
                return InfoBarSeverity.Error;
        }

        return InfoBarSeverity.Informational;
    }
}