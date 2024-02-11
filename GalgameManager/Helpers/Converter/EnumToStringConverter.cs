using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class EnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is not Enum e ? string.Empty : e.GetLocalized();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => default!; //这个功能不需要
}