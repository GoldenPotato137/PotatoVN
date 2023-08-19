using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

[Obsolete("在SDK在14393版本之上，不用此转换器，可以直接传递bool")]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is true ? Visibility.Visible : Visibility.Collapsed;
    
    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is Visibility.Visible;
}
