using GalgameManager.Enums;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class PageToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PageEnum page)
            return page.ToString().GetLocalized();
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => PageEnum.Category; // not need
}