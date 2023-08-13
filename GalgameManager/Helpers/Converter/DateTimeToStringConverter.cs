using System.Globalization;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class DateTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => 
        value is DateTime dateTime && dateTime != DateTime.MinValue ? dateTime.ToString("yyyy-MM-dd") : Galgame.DefaultString;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => 
        DateTime.ParseExact((string)value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}