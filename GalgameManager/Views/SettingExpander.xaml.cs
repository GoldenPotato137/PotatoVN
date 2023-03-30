// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views;
public sealed partial class SettingExpander
{
    public SettingExpander()
    {
        InitializeComponent();
    }
    public string SettingTitle
    {
        get => (string)GetValue(SettingTitleProperty);
        set => SetValue(SettingTitleProperty, value);
    }

    public static readonly DependencyProperty SettingTitleProperty =
        DependencyProperty.Register(nameof(SettingTitle), typeof(string), typeof(SettingExpander), new PropertyMetadata(string.Empty));

    public string SettingDescription
    {
        get => (string)GetValue(SettingDescriptionProperty);
        set => SetValue(SettingDescriptionProperty, value);
    }

    public static readonly DependencyProperty SettingDescriptionProperty =
        DependencyProperty.Register(nameof(SettingDescription), typeof(string), typeof(SettingExpander), new PropertyMetadata(string.Empty));

    public object SettingContent
    {
        get => GetValue(SettingContentProperty);
        set => SetValue(SettingContentProperty, value);
    }
    
    public static readonly DependencyProperty SettingContentProperty =
        DependencyProperty.Register(nameof(SettingContent), typeof(object), typeof(SettingExpander), new PropertyMetadata(null));
    
    public string SettingIcon
    {
        get => (string)GetValue(SettingIconProperty);
        set => SetValue(SettingIconProperty, value);
    }
    
    public static readonly DependencyProperty SettingIconProperty =
        DependencyProperty.Register(nameof(SettingIcon), typeof(string), typeof(SettingExpander), new PropertyMetadata("World"));
}
