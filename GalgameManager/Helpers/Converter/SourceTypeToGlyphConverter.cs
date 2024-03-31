using GalgameManager.Enums;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;
internal class SourceTypeToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            GalgameSourceType.LocalFolder => "\uE8B7",
            GalgameSourceType.LocalZip => "\uF012",
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => GalgameSourceType.UnKnown; //不需要
}
