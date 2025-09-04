using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class ScanResultToGlyph : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            ScanResultType.Information => "\uE946",
            ScanResultType.AlreadyExists => "\uE8C8",
            ScanResultType.Success => "\uE930",
            ScanResultType.Failed => "\uE711",
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ScanResultType.Information; //不需要
}

public class ScanResultToBrush : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            ScanResultType.Information => Application.Current.Resources["SystemFillColorAttentionBackgroundBrush"],
            ScanResultType.AlreadyExists => Application.Current.Resources["SystemFillColorCautionBackgroundBrush"],
            ScanResultType.Success => Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"],
            ScanResultType.Failed => Application.Current.Resources["SystemFillColorCriticalBackgroundBrush"],
            _ => Application.Current.Resources["LayerFillColorDefaultBrush"]
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ScanResultType.Information; //不需要
}

public class ScanResultToString : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ScanResultType type) return type.GetLocalized();
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        ScanResultType.Information; //不需要
}