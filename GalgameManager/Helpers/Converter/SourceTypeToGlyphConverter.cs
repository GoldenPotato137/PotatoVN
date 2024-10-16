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

internal class SourcesToStringConverter : IValueConverter
{
    private readonly SourceTypeToGlyphConverter _sourceTypeToGlyphConverter = new();
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value is not IEnumerable<GalgameSourceBase> sources) return string.Empty;
        IEnumerable<string> tmp = sources.Select(s =>
            _sourceTypeToGlyphConverter.Convert(s.SourceType, targetType, parameter, language) as string ??
            string.Empty);
        return string.Join(" ", tmp);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => null!; //不需要
}