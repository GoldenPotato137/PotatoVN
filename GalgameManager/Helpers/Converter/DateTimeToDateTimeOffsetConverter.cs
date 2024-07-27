using Microsoft.UI.Xaml.Data;
using DateTimeOffset = System.DateTimeOffset;

namespace GalgameManager.Helpers.Converter;

public class DateTimeToDateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, string language)
    {
        if (value is DateTime d)
        {
            if (d == DateTime.MinValue) return null;
            return new DateTimeOffset(d);
        }
        throw new ArgumentException($"{value} is not DateTime");
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        if (value == null) return DateTime.MinValue;
        if (value is DateTimeOffset d) return d.DateTime;
        throw new ArgumentException($"{value} is not DateTimeOffset");
    }
}