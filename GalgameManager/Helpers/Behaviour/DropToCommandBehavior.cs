using Microsoft.UI.Xaml;
using Microsoft.Xaml.Interactivity;

namespace GalgameManager.Helpers.Behaviour;

using System.Windows.Input;

public class DropToCommandBehavior : Behavior<DependencyObject>
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(DropToCommandBehavior), new PropertyMetadata(null));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        ((UIElement)AssociatedObject).AllowDrop = true;
        ((UIElement)AssociatedObject).Drop += OnDrop;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        ((UIElement)AssociatedObject).Drop -= OnDrop;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (Command.CanExecute(e))
        {
            Command.Execute(e);
        }
    }
}
