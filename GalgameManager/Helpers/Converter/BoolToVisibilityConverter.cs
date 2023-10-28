using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(parameter is true or "True" or "true")
            return value is true ? Visibility.Collapsed : Visibility.Visible;
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is Visibility.Visible;
}
