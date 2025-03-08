using DependencyPropertyGenerator;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control;

[DependencyProperty<string>("SettingTitle", DefaultValue = "")]
[DependencyProperty<string>("SettingDescription", DefaultValue = "")]
[DependencyProperty<bool>("IsExpanded", DefaultValue = false)]
[DependencyProperty<string>("SettingIcon", DefaultValue = "&#xE713;")]
[DependencyProperty<Symbol>("SettingSymbol", DefaultValue = Symbol.Accept)]
public sealed partial class SettingExpander
{
    public SettingExpander()
    {
        InitializeComponent();
    }

    public object SettingContent
    {
        get => GetValue(SettingContentProperty);
        set => SetValue(SettingContentProperty, value);
    }
    
    public static readonly DependencyProperty SettingContentProperty =
        DependencyProperty.Register(nameof(SettingContent), typeof(object), typeof(SettingExpander), new PropertyMetadata(null));
}
