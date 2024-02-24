using GalgameManager.Models;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class GameToOpacityConverter : IValueConverter
{
    public static bool SpecialDisplayVirtualGame;
    
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Galgame game)
            return SpecialDisplayVirtualGame && game.CheckExist() == false ? 0.5 : 1;
        return 1;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => true; //不需要
}