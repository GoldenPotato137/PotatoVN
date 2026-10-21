using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

/// <summary>
/// 把下载次数转成紧凑的显示文本，如 999、1.4K、2.5M
/// </summary>
public class DownloadCountToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is long count ? Convert(count) : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => null!; //不需要

    /// 小数部分直接截断而不是四舍五入，避免把下载量往大了显示（如999999不会显示成1000K）
    public static string Convert(long count)
    {
        return count switch
        {
            < 1000 => count.ToString(CultureInfo.InvariantCulture),
            < 1_000_000 => FormatTenths(count / 100) + "K",
            _ => FormatTenths(count / 100_000) + "M",
        };
    }

    /// <param name="tenths">以0.1为单位的整数，如14表示1.4</param>
    private static string FormatTenths(long tenths) =>
        (tenths / 10m).ToString("0.#", CultureInfo.InvariantCulture);
}
