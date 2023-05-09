using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class TimeToDisplayTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var time = (int)value;
        return $"{time} {"Minute".GetLocalized()}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => 0; //不需要
}