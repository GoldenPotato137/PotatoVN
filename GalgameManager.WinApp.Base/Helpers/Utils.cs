using System;
using System.Globalization;

namespace GalgameManager.WinApp.Base.Helpers;

public static class Utils
{
    /// <summary>
    /// 尝试解析日期，若失败则返回DateTime.MinValue
    /// </summary>
    public static DateTime TryParseDateGuessCulture(string dateString)
    {
        CultureInfo[] cultures =
        {
            CultureInfo.InvariantCulture,
            new("en-US"), // MM/dd/yyyy
            new("en-GB"), // dd/MM/yyyy
            new("ja-JP"), // yyyy/MM/dd
            new("zh-CN"), // yyyy/M/d
            // 添加其他可能的文化设置
        };
        foreach (CultureInfo culture in cultures)
            if (DateTime.TryParse(dateString, culture, DateTimeStyles.None, out DateTime parsedDate))
                return parsedDate;
        return DateTime.MinValue;
    }
}