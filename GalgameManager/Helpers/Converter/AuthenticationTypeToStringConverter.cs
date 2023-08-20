using GalgameManager.Enums;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;
internal class AuthenticationTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            AuthenticationType type => type.ToString().GetLocalized(),
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => AuthenticationType.NoAuthentication; //不需要
}
