using System.Globalization;

namespace GalgameManager.Core.Helpers;

public static class DateTimeExtensions
{
    /// <summary>Convert datetime to UNIX time</summary>
    /// <param name="dateTime">日期，UTC/本地时间均可</param>
    public static long ToUnixTime(this DateTime dateTime)
    {
        DateTimeOffset dto = new(dateTime.ToUniversalTime());
        return dto.ToUnixTimeSeconds();
    }
    
    /// <summary>Convert UNIX time to datetime</summary>
    /// <returns>UTC DateTime</returns>
    public static DateTime ToDateTime(this long unixTime)
    {
        DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(unixTime);
        return dto.UtcDateTime;
    }

    public static string ToStringDefault(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy/M/d");
    }

    /// <summary>
    /// 试图将字符串转换为日期，支持yyyy/M/d、yyyy/MM/dd、yyyy-M-d和yyyy-MM-dd格式，失败返回1900年1月1日
    /// </summary>
    /// <param name="dateTime">要转换的日期字符串</param>
    /// <returns>转换后的日期，如果转换失败则返回1900年1月1日</returns>
    public static DateTime ToDateTime(string dateTime)
    {
        string[] formats = { "yyyy/M/d", "yyyy/MM/dd", "yyyy-M-d", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(dateTime, formats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateTime parsedDate))
            return parsedDate;
        
        // 返回一个安全的默认值，默认的DateTime.MinValue会导致navigation出错
        return new DateTime(1900, 1, 1);
    }
}