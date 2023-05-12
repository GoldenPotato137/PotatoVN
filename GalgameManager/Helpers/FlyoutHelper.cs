using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Helpers;

public class FlyoutHelper
{
    public static readonly DependencyProperty CloseFlyoutOnClickProperty =
        DependencyProperty.RegisterAttached("CloseFlyoutOnClick", typeof(bool), typeof(FlyoutHelper), new PropertyMetadata(false, OnCloseFlyoutOnClickChanged));

    public static bool GetCloseFlyoutOnClick(DependencyObject obj)
    {
        return (bool)obj.GetValue(CloseFlyoutOnClickProperty);
    }

    public static void SetCloseFlyoutOnClick(DependencyObject obj, bool value)
    {
        obj.SetValue(CloseFlyoutOnClickProperty, value);
    }

    private static void OnCloseFlyoutOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Button button)
        {
            button.Click -= Button_Click;
            if ((bool)e.NewValue)
            {
                button.Click += Button_Click;
            }
        }
    }

    private static void Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
            button.Flyout.Hide();
    }
}