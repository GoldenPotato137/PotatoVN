using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool bValue)
        {
            return !bValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool bValue)
        {
            return !bValue;
        }
        return true;
    }
}