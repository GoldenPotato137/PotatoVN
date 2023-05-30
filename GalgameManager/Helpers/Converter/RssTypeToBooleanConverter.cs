using GalgameManager.Enums;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class RssTypeToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is RssType type && type == Enum.Parse<RssType>((string)parameter);

    public object ConvertBack(object value, Type targetType, object parameter, string language) => RssType.None; //这个功能不需要
}
