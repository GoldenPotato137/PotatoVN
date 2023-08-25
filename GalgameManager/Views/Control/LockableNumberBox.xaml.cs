// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Globalization.NumberFormatting;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views.Control;

public sealed partial class LockableNumberBox : INotifyPropertyChanged
{
    public LockableNumberBox()
    {
        InitializeComponent();
        IncrementNumberRounder rounder = new()
        {
            Increment = 0.1,
            RoundingAlgorithm = RoundingAlgorithm.RoundUp
        };

        DecimalFormatter formatter = new()
        {
            IntegerDigits = 1,
            FractionDigits = 1,
            NumberRounder = rounder
        };
        FormattedNumberBox.NumberFormatter = formatter;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(LockableNumberBox), new PropertyMetadata(string.Empty));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(LockableNumberBox), new PropertyMetadata(0.0D));
    
    public bool Readonly
    {
        get => (bool)GetValue(ReadonlyProperty);
        set => SetValue(ReadonlyProperty, value);
    }

    public static readonly DependencyProperty ReadonlyProperty =
        DependencyProperty.Register(nameof(Readonly), typeof(bool), typeof(LockableNumberBox), new PropertyMetadata(false));

    public bool IsLock
    {
        get => (bool)GetValue(IsLockProperty);

        set
        {
            SetValue(IsLockProperty, value);
            OnPropertyChanged(nameof(IsEditable));
        }
    }

    public static readonly DependencyProperty IsLockProperty =
        DependencyProperty.Register(nameof(IsLock), typeof(bool), typeof(LockableNumberBox), new PropertyMetadata(false));
    
    public bool IsEditable => !IsLock & !Readonly;
        
        
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}