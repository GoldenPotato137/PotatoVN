// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using GalgameManager.Views.Control;

using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views;
public sealed partial class SettingToggleSwitch
{
    public SettingToggleSwitch()
    {
        InitializeComponent();
    }
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingExpander), new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingExpander), new PropertyMetadata(string.Empty));

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }
    
    public static readonly DependencyProperty IsOnProperty =
        DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(SettingExpander), new PropertyMetadata(false));
}
