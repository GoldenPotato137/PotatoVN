using GalgameManager.Views.Control.TokenizingTextBox;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class InterspersedObservableCollectionConverter:IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is InterspersedObservableCollection collection)
        {
            return collection.ItemsSource;
        }

        return value;
    }
}