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
            App.GetService<IInfoService>().DeveloperEvent("value is not number", InfoBarSeverity.Error);
            return string.Empty;
        }
        
        var time = (int)value;
        return Convert(time);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => 0; //不需要

    public static string Convert(int value)
    {
        var timeAsHour = App.GetService<ILocalSettingsService>().ReadSettingAsync<bool>(KeyValues.TimeAsHour).Result;
        if (timeAsHour)
            return value > 60 ? $"{value / 60}h{value % 60}m" : $"{value}m";
        return $"{value} {"Minute".GetLocalized()}";
    }
}