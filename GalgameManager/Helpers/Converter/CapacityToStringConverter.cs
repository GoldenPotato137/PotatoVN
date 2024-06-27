using GalgameManager.Contracts.Services;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class CapacityToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        long number = -1;
        if (value is long num) number = num;
        if (value is double num2) number = (long)num2;
        if (number == -1)
        {
            try
            {
                number = long.Parse(value.ToString()!);
            }
            catch (Exception e)
            {
                App.GetService<IInfoService>().DeveloperEvent($"Cannot convert capacity to string with exception: {e}");
                return "Unknown";
            }
        }
        return Convert(number);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => 0; //不需要

    public static string Convert(long capacity)
    {
        return capacity switch
        {
            < 1024 => $"{capacity} B",
            < 1024 * 1024 => $"{capacity / 1024.0:F2} KB",
            < 1024 * 1024 * 1024 => $"{capacity / 1024.0 / 1024.0:F2} MB",
            _ => $"{capacity / 1024.0 / 1024.0 / 1024.0:F2} GB"
        };
    }
}