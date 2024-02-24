using Microsoft.UI.Xaml;

namespace GalgameManager.Helpers;

public static class BoolExtensions
{
    public static Visibility ToVisibility(this bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}