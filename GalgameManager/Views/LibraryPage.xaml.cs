using GalgameManager.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GalgameManager.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        ViewModel = App.GetService<LibraryViewModel>();
        InitializeComponent();
    }

    // 并不MVVM，但我想不出更好的方案
    private void UIElement_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties? properties = e.GetCurrentPoint(sender as UIElement).Properties;
        if (properties.IsXButton1Pressed)
        {
            ViewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
        else if (properties.IsXButton2Pressed)
        {
            ViewModel.ForwardCommand.Execute(null);
            e.Handled = true;
        }
    }
}