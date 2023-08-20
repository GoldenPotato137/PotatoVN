using Windows.UI;
using GalgameManager.Enums;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Helpers.Converter;

public class PlayTypeToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Color tmp =  value is PlayType playType ? playType.ToColor() : PlayType.None.ToColor();
        return new SolidColorBrush(tmp);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => PlayType.None; // Not used
}