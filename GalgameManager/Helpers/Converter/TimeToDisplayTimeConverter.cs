using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class TimeToDisplayTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int && value is not long)
        {
            App.GetService<IInfoService>().DeveloperEvent(InfoBarSeverity.Error, "value is not number");
            return string.Empty;
        }
        
        int time = value is long longValue
            ? checked((int)Math.Min(int.MaxValue, longValue))
            : (int)value;
        return Convert(time);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => 0; //不需要

    public static string Convert(int value)
    {
        var timeAsHour = App.GetService<ILocalSettingsService>().ReadSettingAsync<bool>(KeyValues.TimeAsHour).Result;
        return ConvertMinutes(value, timeAsHour);
    }

    public static string ConvertSeconds(long value)
    {
        bool timeAsHour = App.GetService<ILocalSettingsService>()
            .ReadSettingAsync<bool>(KeyValues.TimeAsHour).Result;
        return ConvertSeconds(value, timeAsHour);
    }

    public static string ConvertMinutes(int value, bool timeAsHour)
    {
        int minutes = Math.Max(0, value);
        if (timeAsHour)
            return minutes > 60 ? $"{minutes / 60}h{minutes % 60}m" : $"{minutes}m";
        return $"{minutes} {"Minute".GetLocalized()}";
    }

    public static string ConvertWholeMinutesWithUnits(long value, bool timeAsHour)
    {
        long minutes = Math.Max(0, value);
        long hours = minutes / 60;
        if (timeAsHour && hours > 0)
            return $"{hours} {"Hour".GetLocalized()} {minutes % 60} {"Minute".GetLocalized()}";
        return $"{minutes} {"Minute".GetLocalized()}";
    }

    /// <summary>
    /// 将秒数按分钟模式的原版格式显示。正数不足一分钟时保留其存在性，但不会虚增为一分钟。
    /// </summary>
    public static string ConvertMinuteModeSeconds(long value, bool timeAsHour)
    {
        long seconds = Math.Max(0, value);
        if (seconds is > 0 and < 60)
            return timeAsHour ? "<1m" : "LessThanOneMinute".GetLocalized();
        return ConvertMinutes(
            checked((int)Math.Min(int.MaxValue, seconds / 60)),
            timeAsHour);
    }

    /// <summary>
    /// 将秒数按本地化的小时／分钟单位显示，不展示秒级余数。
    /// </summary>
    public static string ConvertWholeMinuteSecondsWithUnits(long value, bool timeAsHour)
    {
        long seconds = Math.Max(0, value);
        return seconds is > 0 and < 60
            ? "LessThanOneMinute".GetLocalized()
            : ConvertWholeMinutesWithUnits(seconds / 60, timeAsHour);
    }

    public static string ConvertSeconds(long value, bool timeAsHour)
    {
        long seconds = Math.Max(0, value);
        long hours = seconds / 3600;
        long minutes = seconds % 3600 / 60;
        long remainder = seconds % 60;
        if (timeAsHour && hours > 0)
            return $"{hours} {"Hour".GetLocalized()} {minutes} {"Minute".GetLocalized()} {remainder} {"Second".GetLocalized()}";
        if (!timeAsHour)
        {
            long totalMinutes = seconds / 60;
            return totalMinutes > 0
                ? $"{totalMinutes} {"Minute".GetLocalized()} {remainder} {"Second".GetLocalized()}"
                : $"{remainder} {"Second".GetLocalized()}";
        }
        if (minutes > 0)
            return $"{minutes} {"Minute".GetLocalized()} {remainder} {"Second".GetLocalized()}";
        return $"{remainder} {"Second".GetLocalized()}";
    }
}
