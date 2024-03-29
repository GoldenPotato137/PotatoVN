using GalgameManager.Models;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GalgameManager.Helpers.Converter;

public class ImagePathConverter : IValueConverter
{
    /// <summary>
    /// 将图片路径转换为BitmapImage，若路径为空或null则返回默认图片
    /// </summary>
    /// <param name="value">路径</param>
    /// <param name="targetType">不使用此参数</param>
    /// <param name="parameter">默认图片路径</param>
    /// <param name="language">不使用此参数</param>
    /// <returns></returns>
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