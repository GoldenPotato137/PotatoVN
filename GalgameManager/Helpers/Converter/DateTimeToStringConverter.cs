using System.Globalization;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Data;
using System;

namespace GalgameManager.Helpers.Converter;

public class DateTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTime dateTime || dateTime == DateTime.MinValue)
        {
            return "-";
        }
        
        // 当年份为1年时，只显示月日
        if (dateTime.Year == 1)
        {
            return dateTime.ToString("MM-dd");
        }
        
        return dateTime.ToString("yyyy-MM-dd");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string str && DateTime.TryParse(str, out var result))
        {
            return result;
        }
        return DateTime.MinValue;
    }
}