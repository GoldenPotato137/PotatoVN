using GalgameManager.Enums;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class WindowModeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return ("WindowMode_" + (WindowMode)value).GetLocalized();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => WindowMode.Normal; // 不需要
}