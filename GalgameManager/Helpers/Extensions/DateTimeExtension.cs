using System.Globalization;

namespace GalgameManager.Helpers;

public static class DateTimeExtensions
{
    // Convert datetime to UNIX time
    public static long ToUnixTime(this DateTime dateTime)
    {
        DateTimeOffset dto = new DateTimeOffset(dateTime.ToUniversalTime());
        return dto.ToUnixTimeSeconds();
    }
    
    // Convert UNIX time to datetime
    public static DateTime ToDateTime(this long unixTime)
    {
        DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(unixTime);
        return dto.DateTime;
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