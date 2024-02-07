namespace GalgameManager.Server.Helpers;

public static class DateTimeExtensions
{
    // Convert datetime to UNIX time
    public static long ToUnixTime(this DateTime dateTime)
    {
        DateTimeOffset dto = new DateTimeOffset(dateTime.ToUniversalTime());
        return dto.ToUnixTimeSeconds();
    }
}