using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;
using GalgameManager.Views.Control;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.WinUI;
using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;

namespace GalgameManager.Views.Dialog;

public sealed partial class KeyMappingDialog : ContentDialog
{
    public GalgameSettingViewModel ViewModel { get; }
    public ObservableCollection<KeyMapping> DialogKeyMappings { get; private set; } = null!;
    public bool IsGlobalMappingEditorRequested { get; private set; }
    private readonly ObservableCollection<KeyMapping> _originalKeyMappings;

    public static readonly DependencyProperty HasMappingsProperty =
        DependencyProperty.Register(nameof(HasMappings), typeof(bool), typeof(KeyMappingDialog), new PropertyMetadata(false));

    public bool HasMappings
    {
        get => (bool)GetValue(HasMappingsProperty);
        private set => SetValue(HasMappingsProperty, value);
    }

    public static readonly DependencyProperty InputModeProperty =
        DependencyProperty.Register(nameof(InputMode), typeof(InputMode), typeof(KeyMappingDialog), new PropertyMetadata(InputMode.Capture, OnInputModeChanged));

    public InputMode InputMode
    {
        get => (InputMode)GetValue(InputModeProperty);
        set => SetValue(InputModeProperty, value);
    }

    private static void OnInputModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyMappingDialog dialog)
        {
            dialog.UpdateAllInputBoxes((InputMode)e.NewValue);
        }
    }

    public KeyMappingDialog(GalgameSettingViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Title = "KeyMappingDialog_Title".GetLocalized();
        PrimaryButtonText = "KeyMappingDialog_Button_Save".GetLocalized();
        CloseButtonText = "KeyMappingDialog_Button_Close".GetLocalized();

        // 创建原始数据的深拷贝，完全不修改 ViewModel 的数据
        _originalKeyMappings = new ObservableCollection<KeyMapping>(ViewModel.KeyMappings);
        DialogKeyMappings = CreateDeepCopy(_originalKeyMappings);

        // 初始化 HasMappings 状态
        UpdateHasMappings();

        // 添加PrimaryButtonClick事件处理
        PrimaryButtonClick += OnSaveButtonClick;
        Loaded += async (_, _) => await UpdateKeyMappingActivationStatusAsync();
    }

    private async Task UpdateKeyMappingActivationStatusAsync()
    {
        bool globalEnabled = await App.GetService<ILocalSettingsService>()
            .ReadSettingAsync<bool>(KeyValues.GameReMapEnabled);
        bool gameEnabled = ViewModel.Gal.KeyReMap;

        if (!globalEnabled && !gameEnabled)
        {
            KeyMappingActivationInfoBar.Severity = InfoBarSeverity.Warning;
            KeyMappingActivationInfoBar.Title = "KeyMappingDialog_Activation_Disabled_Title".GetLocalized();
            KeyMappingActivationInfoBar.Message = "KeyMappingDialog_Activation_Disabled_Message".GetLocalized();
        }
        else
        {
            KeyMappingActivationInfoBar.Severity = InfoBarSeverity.Success;
            KeyMappingActivationInfoBar.Title = "KeyMappingDialog_Activation_Enabled_Title".GetLocalized();
            KeyMappingActivationInfoBar.Message = (gameEnabled, globalEnabled) switch
            {
                (true, true) => "KeyMappingDialog_Activation_Enabled_Both_Message".GetLocalized(),
                (true, false) => "KeyMappingDialog_Activation_Enabled_Game_Message".GetLocalized(),
                _ => "KeyMappingDialog_Activation_Enabled_Global_Message".GetLocalized(),
            };
        }

        EnableGameKeyMappingButton.Content = (gameEnabled
            ? GetButtonLabel("KeyMappingDialog_Activation_DisableGame_Label", "停用本游戏映射")
            : GetButtonLabel("KeyMappingDialog_Activation_EnableGame_Label", "启用本游戏映射"));
        EnableGlobalKeyMappingButton.Content = (globalEnabled
            ? GetButtonLabel("KeyMappingDialog_Activation_DisableGlobal_Label", "停止为所有游戏启用")
            : GetButtonLabel("KeyMappingDialog_Activation_EnableGlobal_Label", "为所有游戏启用"));
        EnableGameKeyMappingButton.Visibility = Visibility.Visible;
        EnableGlobalKeyMappingButton.Visibility = Visibility.Visible;
    }

    private static string GetButtonLabel(string resourceKey, string fallback)
    {
        string? localized = resourceKey.GetLocalized();
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }

    private async void EnableGameKeyMappingButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Gal.KeyReMap = !ViewModel.Gal.KeyReMap;
        await UpdateKeyMappingActivationStatusAsync();
    }

    private async void EnableGlobalKeyMappingButton_Click(object sender, RoutedEventArgs e)
    {
        ILocalSettingsService settings = App.GetService<ILocalSettingsService>();
        bool enabled = await settings.ReadSettingAsync<bool>(KeyValues.GameReMapEnabled);
        await settings.SaveSettingAsync(KeyValues.GameReMapEnabled, !enabled);
        await UpdateKeyMappingActivationStatusAsync();
    }

    private void EditGlobalKeyMappingButton_Click(object sender, RoutedEventArgs e)
    {
        IsGlobalMappingEditorRequested = true;
        Hide();
    }

    public bool ConsumeGlobalMappingEditorRequest()
    {
        if (!IsGlobalMappingEditorRequested) return false;
        IsGlobalMappingEditorRequested = false;
        return true;
    }

    public void RefreshGlobalMappings(IEnumerable<KeyMapping> globalMappings)
    {
        List<KeyMapping> persistedGameMappings =
            GalgameManager.Helpers.KeyMappingMergeHelper.BuildPersistedGameMappings(DialogKeyMappings);
        List<KeyMapping> refreshedMappings =
            GalgameManager.Helpers.KeyMappingMergeHelper.BuildEffectiveMappings(
                persistedGameMappings, globalMappings);

        DialogKeyMappings.Clear();
        foreach (KeyMapping mapping in refreshedMappings)
            DialogKeyMappings.Add(GalgameManager.Helpers.KeyMappingMergeHelper.Clone(mapping));
        UpdateHasMappings();
    }

    private ObservableCollection<KeyMapping> CreateDeepCopy(ObservableCollection<KeyMapping> source)
    {
        var copy = new ObservableCollection<KeyMapping>();
        foreach (var mapping in source)
        {
            copy.Add(new KeyMapping
            {
                Remark = mapping.Remark,
                From = mapping.From?.ToList() ?? [],
                To = mapping.To?.ToList() ?? [],
                IsEnabled = mapping.IsEnabled,
                IsGlobal = mapping.IsGlobal
            });
        }
        return copy;
    }

    private void RemoveKeyMapping(KeyMapping? mapping)
    {
        if (mapping is { IsGlobal: false })
        {
            DialogKeyMappings.Remove(mapping);
            UpdateHasMappings();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is KeyMapping mapping)
        {
            RemoveKeyMapping(mapping);
        }
    }

    private void OnSaveButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        List<KeyMapping> localMappings = DialogKeyMappings.Where(mapping => !mapping.IsGlobal).ToList();
        if (!GalgameManager.Helpers.KeyMappingMergeHelper.HasDuplicateEnabledSources(localMappings)) return;

        args.Cancel = true;
        KeyMappingActivationInfoBar.Severity = InfoBarSeverity.Error;
        KeyMappingActivationInfoBar.Title = "KeyMappingDialog_DuplicateSource_Title".GetLocalized();
        KeyMappingActivationInfoBar.Message = "KeyMappingDialog_DuplicateSource_Message".GetLocalized();
    }

    private void AddKeyMapping()
    {
        DialogKeyMappings.Add(new KeyMapping { IsGlobal = false });
        UpdateHasMappings();
    }

    private void AddKeyMappingButton_Click(object sender, RoutedEventArgs e)
    {
        AddKeyMapping();
        DispatcherQueue.TryEnqueue(() =>
        {
            MappingScrollViewer.UpdateLayout();
            MappingScrollViewer.ChangeView(null, MappingScrollViewer.ScrollableHeight, null);
        });
    }

    private void UpdateHasMappings()
    {
        HasMappings = DialogKeyMappings.Count > 0;

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
}
