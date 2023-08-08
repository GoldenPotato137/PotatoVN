using GalgameManager.Models;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GalgameManager.Helpers.Converter;

public class ImagePathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
            return new BitmapImage(new Uri(str));
        if (parameter is string para)
            return new BitmapImage(new Uri(para));
        return new BitmapImage(new Uri(Galgame.DefaultImagePath));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => string.Empty; //No needed
}