using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class InverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Visibility visibility
            ? visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed
            : throw new ArgumentException("Value must be Visibility type.");

    public object ConvertBack(object value, Type targetType, object parameter, string language) => Visibility.Visible; // Not used
}