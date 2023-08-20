using GalgameManager.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class RssTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var tmp =  value is RssType type && type == Enum.Parse<RssType>((string)parameter);
        return tmp ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => RssType.None; //这个功能不需要
}
