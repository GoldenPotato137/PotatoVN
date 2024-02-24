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
    /// 试图将字符串转换为日期，支持yyyy/M/d和yyyy/MM/dd两种格式，失败返回DateTime.MinValue
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime ToDateTime(string dateTime)
    {
        string[] formats = { "yyyy/M/d", "yyyy/MM/dd" };
        if (DateTime.TryParseExact(dateTime, formats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateTime parsedDate))
            return parsedDate;
        return DateTime.MinValue;
    }
}