// https://gist.github.com/tomtom-m/4df733e1fdb68291ed4a5100629c11c4
// 等待WinAppSdk1.5更新修复stackPanel问题后删除

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control;

/// <summary>
/// Override of StackPanel which fixes a spacing problem.
/// https://github.com/microsoft/microsoft-ui-xaml/issues/916
/// The original StackPanel applies spacing to collapsed items.
/// This implementation doesn't.
/// </summary>
public class StackPanelWithSpacing : StackPanel
{
    /// <summary>
    /// Gets or sets the space between visible elements.
    /// </summary>
    public static readonly DependencyProperty SpaceProperty =
        DependencyProperty.Register(nameof(Space), typeof(int), typeof(StackPanelWithSpacing), new PropertyMetadata(0));

    /// <summary>
    /// Initializes a new instance of the <see cref="StackPanelWithSpacing"/> class.
    /// </summary>
    public StackPanelWithSpacing()
    {
        this.Loaded += this.StackPanelWithSpacing_Loaded;
    }

    /// <summary>
    /// Gets or sets the space between visible elements.
    /// </summary>
    public int Space
    {
        get => (int)this.GetValue(SpaceProperty);
        set => this.SetValue(SpaceProperty, value);
    }

    private void StackPanelWithSpacing_Loaded(object sender, object e)
        => this.SetSpacingForChildren(this.Space);

    private void SetSpacingForChildren(int spacing)
    {
        for (int i = 0; i < this.Children.Count; i++)
        {
            if (this.Children[i] is FrameworkElement element
                && element.Visibility == Visibility.Visible)
            {
                var halfSpacing = spacing / 2;
                var topSpacing = i == 0 ? 0 : halfSpacing;

                element.Margin = new Thickness(element.Margin.Left, element.Margin.Top + topSpacing, element.Margin.Right, element.Margin.Bottom + halfSpacing);
            }
        }
    }
}