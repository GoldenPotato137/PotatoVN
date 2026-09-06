
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using GalgameManager.Models;
using GalgameManager.Views.Control;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.WinUI;

namespace GalgameManager.Views.Dialog;

public sealed partial class GlobalKeyMappingDialog : ContentDialog, INotifyPropertyChanged
{
    private ObservableCollection<KeyMapping> _mappings = null!;
    private bool _hasMappings;

    public static readonly DependencyProperty InputModeProperty =
        DependencyProperty.Register(nameof(InputMode), typeof(InputMode), typeof(GlobalKeyMappingDialog), new PropertyMetadata(InputMode.Capture, OnInputModeChanged));

    public InputMode InputMode
    {
        get => (InputMode)GetValue(InputModeProperty);
        set => SetValue(InputModeProperty, value);
    }

    private static void OnInputModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlobalKeyMappingDialog dialog)
        {
            dialog.UpdateAllInputBoxes((InputMode)e.NewValue);
        }
    }

    public ObservableCollection<KeyMapping> Mappings
    {
        get => _mappings;
        private set
        {
            _mappings = value;
            OnPropertyChanged();
        }
    }

    public bool HasMappings
    {
        get => _hasMappings;
        private set
        {
            if (_hasMappings == value) return;
            _hasMappings = value;
            OnPropertyChanged();
        }
    }

    public List<KeyMapping> ResultMappings => Mappings.ToList();

    public GlobalKeyMappingDialog(IEnumerable<KeyMapping> mappings)
    {
        InitializeComponent();

        // 设置对话框基本信息
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Title = "GlobalKeyMappingDialog_Title".GetLocalized();
        PrimaryButtonText = "GlobalKeyMappingDialog_Button_Save".GetLocalized();
        SecondaryButtonText = "GlobalKeyMappingDialog_Button_Clear".GetLocalized();
        CloseButtonText = "GlobalKeyMappingDialog_Button_Cancel".GetLocalized();
        GlobalMappingInfoBar.Message = "GlobalKeyMappingDialog_Description".GetLocalized();

        Mappings = new ObservableCollection<KeyMapping>();
        foreach (var mapping in mappings)
        {
            Mappings.Add(new KeyMapping
            {
                Remark = mapping.Remark,
                From = mapping.From is null ? [] : [.. mapping.From],
                To = mapping.To is null ? [] : [.. mapping.To],
                IsEnabled = mapping.IsEnabled,
                IsGlobal = true
            });
        }

        UpdateHasMappings();
        Mappings.CollectionChanged += (_, _) =>
        {
            UpdateHasMappings();

            // 如果没有映射了，自动切换回捕获模式（关闭模式）
            if (!HasMappings && InputMode == InputMode.Dropdown)
            {
                InputMode = InputMode.Capture;
                // 同步 ToggleSwitch 状态
                if (InputModeToggle != null)
                {
                    InputModeToggle.IsOn = false;
                }
            }
        };

        // 清空按钮逻辑：直接清空并返回Secondary结果，让ViewModel处理保存
        SecondaryButtonClick += (_, _) => Mappings.Clear();
        PrimaryButtonClick += ValidateMappings;
    }

    private void UpdateHasMappings()
    {
        HasMappings = Mappings.Count > 0;
    }

    private void AddMapping()
    {
        Mappings.Add(new KeyMapping { IsGlobal = true });
    }

    private void AddMappingButton_Click(object sender, RoutedEventArgs e)
    {
        AddMapping();
        DispatcherQueue.TryEnqueue(() =>
        {
            MappingScrollViewer.UpdateLayout();
            MappingScrollViewer.ChangeView(null, MappingScrollViewer.ScrollableHeight, null);
        });
    }

    private void ValidateMappings(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        bool hasDuplicateSource = GalgameManager.Helpers.KeyMappingMergeHelper.HasDuplicateEnabledSources(Mappings);
        if (!hasDuplicateSource) return;

        args.Cancel = true;
        GlobalMappingInfoBar.Severity = InfoBarSeverity.Error;
        GlobalMappingInfoBar.Message = "GlobalKeyMappingDialog_DuplicateSource".GetLocalized();
    }

    private void RemoveMapping(KeyMapping? mapping)
    {
        if (mapping != null)
        {
            Mappings.Remove(mapping);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is KeyMapping mapping)
        {
            RemoveMapping(mapping);
        }
    }

    private void InputModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            InputMode = toggleSwitch.IsOn ? InputMode.Dropdown : InputMode.Capture;
        }
    }

    private void UpdateAllInputBoxes(InputMode mode)
    {
        // 遍历所有FlexibleHotkeyInputBox并更新它们的模式
        var inputBoxes = FindVisualChildren<FlexibleHotkeyInputBox>(this);
        foreach (var inputBox in inputBoxes)
        {
            inputBox.InputMode = mode;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
    {
        if (depObj != null)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject? child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                {
                    yield return (T)child;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
