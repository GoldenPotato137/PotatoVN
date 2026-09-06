using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using GalgameManager.Helpers;

namespace GalgameManager.Views.Control;

public enum InputMode
{
    Capture,
    Dropdown
}

public sealed partial class FlexibleHotkeyInputBox : INotifyPropertyChanged
{
    private readonly HashSet<VirtualKey> _pressedKeys = new();
    private readonly HashSet<int> _mouseButtons = new();
    private bool _isCapturing;
    private bool _leftPointerClickPending;
    private bool _leftPointerCompletedCapture;
    private string _buttonText = "";
    private string _hotkeyDisplayText = "";
    private Visibility _showPlaceholder = Visibility.Visible;
    private Visibility _hideText = Visibility.Collapsed;
    private Visibility _captureButtonVisibility = Visibility.Visible;
    private Visibility _dropdownVisibility = Visibility.Collapsed;

    public FlexibleHotkeyInputBox()
    {
        InitializeComponent();
        // Button 会把 Enter/Space 当成 Click 并在类处理器中标记按键事件为已处理。
        // handledEventsToo=true 能让捕获器仍然收到真实键盘事件。
        InputButton.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(InputButton_KeyDown), true);
        InputButton.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(InputButton_KeyUp), true);
        // Button 同样会在类处理器中吞掉 PointerPressed；必须监听已处理事件，
        // 否则普通左键点击无法留下“来自鼠标”的标记，捕获模式就无法启动。
        InputButton.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler(InputButton_PointerPressed), true);
        UpdateButtonText();

        // 延迟初始化模式显示，确保控件完全加载
        Loaded += (sender, e) =>
        {
            // 如果没有通过 XAML 绑定设置 InputMode，尝试自动查找父级的 InputMode
            if (InputMode == InputMode.Capture)
            {
                var parentInputMode = FindParentInputMode(this);
                if (parentInputMode.HasValue)
                {
                    InputMode = parentInputMode.Value;
                }
            }
            UpdateDisplay();
        };
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(FlexibleHotkeyInputBox),
            new PropertyMetadata(""));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty HotkeyKeysProperty =
        DependencyProperty.Register(nameof(HotkeyKeys), typeof(List<int>), typeof(FlexibleHotkeyInputBox),
            new PropertyMetadata(new List<int>(), OnHotkeyKeysChanged));

    public List<int> HotkeyKeys
    {
        get => (List<int>)GetValue(HotkeyKeysProperty);
        set => SetValue(HotkeyKeysProperty, value);
    }

    public static readonly DependencyProperty InputModeProperty =
        DependencyProperty.Register(nameof(InputMode), typeof(InputMode), typeof(FlexibleHotkeyInputBox),
            new PropertyMetadata(InputMode.Capture, OnInputModeChanged));

    public InputMode InputMode
    {
        get => (InputMode)GetValue(InputModeProperty);
        set => SetValue(InputModeProperty, value);
    }

    private static void OnInputModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlexibleHotkeyInputBox control)
        {
            control.UpdateModeDisplay();
        }
    }

    private static void OnHotkeyKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlexibleHotkeyInputBox control)
        {
            // 根据当前模式更新显示
            if (control.InputMode == InputMode.Dropdown)
            {
                // 在下拉模式下，先更新选择，然后更新显示文本
                control.UpdateDropdownSelection();
                control.UpdateDisplay();
            }
            else
            {
                // 在捕获模式下，正常更新显示
                control.UpdateDisplay();
            }
        }
    }

    public string ButtonText
    {
        get => _buttonText;
        private set
        {
            _buttonText = value;
            OnPropertyChanged();
        }
    }

    public string HotkeyDisplayText
    {
        get => _hotkeyDisplayText;
        private set
        {
            _hotkeyDisplayText = value;
            OnPropertyChanged();
        }
    }

    public Visibility ShowPlaceholder
    {
        get => _showPlaceholder;
        private set
        {
            _showPlaceholder = value;
            OnPropertyChanged();
        }
    }

    public Visibility HideText
    {
        get => _hideText;
        private set
        {
            _hideText = value;
            OnPropertyChanged();
        }
    }

    public Visibility CaptureButtonVisibility
    {
        get => _captureButtonVisibility;
        private set
        {
            _captureButtonVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility DropdownVisibility
    {
        get => _dropdownVisibility;
        private set
        {
            _dropdownVisibility = value;
            OnPropertyChanged();
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    
    private void InputButton_Click(object sender, RoutedEventArgs e)
    {
        // Button.Click 既可能来自鼠标，也可能来自 Enter/Space。只有前面确实收到左键
        // PointerPressed 时才把它当作鼠标交互，避免 Enter 被误录成 MOUSE_LEFT。
        if (!_leftPointerClickPending) return;

        _leftPointerClickPending = false;
        if (_leftPointerCompletedCapture)
        {
            _leftPointerCompletedCapture = false;
            return;
        }

        if (!_isCapturing) StartCapturing();
    }

    private void InputButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturing)
        {
            // 保留键盘可访问性：第一次 Enter/Space 进入捕获，第二次才把该键录入。
            if (e.Key is VirtualKey.Enter or VirtualKey.Space)
            {
                e.Handled = true;
                StartCapturing();
            }
            return;
        }

        e.Handled = true;
        VirtualKey key = ResolvePhysicalModifier(e);

        // 修饰键先保留，等到普通键按下时一起完成捕获；这样可以录入 Ctrl+K 一类组合键。
        _pressedKeys.Add(key);
        UpdateCaptureDisplay();

        if (!IsModifierKey(key))
        {
            CompleteCapture();
        }
    }

    private void InputButton_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        VirtualKey key = ResolvePhysicalModifier(e);
        if (!_isCapturing || !IsModifierKey(key)) return;

        e.Handled = true;
        // 允许单独映射修饰键：若用户只按了一个或多个修饰键，在第一个松开事件到来时完成。
        if (_pressedKeys.Count > 0 && _pressedKeys.All(IsModifierKey))
        {
            CompleteCapture();
        }
    }

    private void InputButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointerPoint = e.GetCurrentPoint(InputButton);
        var properties = pointerPoint.Properties;

        if (properties.IsLeftButtonPressed)
        {
            _leftPointerClickPending = true;
            _leftPointerCompletedCapture = _isCapturing;
            if (_isCapturing)
            {
                _mouseButtons.Add(1);
                CompleteCapture();
            }
            return;
        }

        if (!_isCapturing) return;

        if (properties.IsRightButtonPressed)
        {
            _mouseButtons.Add(2);
            CompleteCapture();
        }
        else if (properties.IsMiddleButtonPressed)
        {
            _mouseButtons.Add(3);
            CompleteCapture();
        }
        else if (properties.IsXButton1Pressed)
        {
            _mouseButtons.Add(4);
            CompleteCapture();
        }
        else if (properties.IsXButton2Pressed)
        {
            _mouseButtons.Add(5);
            CompleteCapture();
        }
    }

    private void InputButton_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!_isCapturing) return;

        var pointerPoint = e.GetCurrentPoint(InputButton);
        var properties = pointerPoint.Properties;

        if (properties.MouseWheelDelta != 0)
        {
            // 区分滚轮方向：向上为6，向下为7
            var wheelCode = properties.MouseWheelDelta > 0 ? 6 : 7;
            _mouseButtons.Add(wheelCode);
            CompleteCapture();
        }
    }

    private void InputButton_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isCapturing)
        {
            CancelCapture();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        HotkeyKeys = new List<int>();
        OnHotkeyChanged();
    }

    private void StartCapturing()
    {
        _isCapturing = true;
        _pressedKeys.Clear();
        _mouseButtons.Clear();
        ButtonText = "KeyMappingDialog_Placeholder_PressKey".GetLocalized() ?? "请按下快捷键或鼠标按键";
        ShowPlaceholder = Visibility.Collapsed;
        HideText = Visibility.Collapsed;
        InputButton.Focus(FocusState.Programmatic);
    }

    private void CompleteCapture()
    {
        _isCapturing = false;

        List<int> keys = new();

        // 添加键盘按键
        keys.AddRange(_pressedKeys.OrderBy(GetKeyOrder).Select(k => (int)k));

        // 添加鼠标按键（如果有）
        if (_mouseButtons.Count > 0)
        {
            // 如果捕获到鼠标按键，只保留第一个鼠标按键
            keys.Add(_mouseButtons.First());
        }

        HotkeyKeys = keys;

        UpdateDisplay();
        OnHotkeyChanged();
    }

    private void CancelCapture()
    {
        _isCapturing = false;
        UpdateDisplay();
    }

    private static VirtualKey ResolvePhysicalModifier(KeyRoutedEventArgs e)
    {
        // 某些 WinUI 键盘事件只给出通用 Shift/Ctrl，需要通过扫描码和扩展位还原左右键。
        // 已经给出左右键时原样保留，以兼容不同 Windows App SDK 版本。
        return e.Key switch
        {
            VirtualKey.Shift => e.KeyStatus.ScanCode == 0x36
                ? VirtualKey.RightShift
                : VirtualKey.LeftShift,
            VirtualKey.Control => e.KeyStatus.IsExtendedKey
                ? VirtualKey.RightControl
                : VirtualKey.LeftControl,
            VirtualKey.Menu => e.KeyStatus.IsExtendedKey
                ? VirtualKey.RightMenu
                : VirtualKey.LeftMenu,
            _ => e.Key,
        };
    }

    private void UpdateCaptureDisplay()
    {
        if (_isCapturing)
        {
            List<string> displayParts = new();

            if (_pressedKeys.Count > 0)
            {
                IOrderedEnumerable<VirtualKey> sortedKeys = _pressedKeys.OrderBy(GetKeyOrder);
                displayParts.AddRange(sortedKeys.Select(GetKeyDisplayName));
            }

            if (_mouseButtons.Count > 0)
            {
                displayParts.Add(GetMouseDisplayName(_mouseButtons.First()));
            }

            if (displayParts.Count > 0)
            {
                ButtonText = string.Join(" + ", displayParts);
            }
        }
    }

    private void UpdateDisplay()
    {
        if (HotkeyKeys.Count == 0)
        {
            ButtonText = "";
            HotkeyDisplayText = "";
            ShowPlaceholder = Visibility.Visible;
            HideText = Visibility.Collapsed;
        }
        else
        {
            var displayParts = new List<string>();

            foreach (var keyCode in HotkeyKeys)
            {
                if (IsMouseKeyCode(keyCode))
                {
                    displayParts.Add(GetMouseDisplayName(keyCode));
                }
                else
                {
                    displayParts.Add(GetKeyDisplayName((VirtualKey)keyCode));
                }
            }

            // 排序：键盘按键在前，鼠标按键在后
            var sortedParts = displayParts
                .Select((part, index) => new { Part = part, Index = index, IsMouse = IsMouseKeyCode(HotkeyKeys[index]) })
                .OrderBy(x => x.IsMouse ? 1 : 0)
                .ThenBy(x => x.Index)
                .Select(x => x.Part);

            var displayText = string.Join(" + ", sortedParts);

            if (_isCapturing)
            {
                ButtonText = displayText;
                HotkeyDisplayText = "";
                ShowPlaceholder = Visibility.Collapsed;
                HideText = Visibility.Collapsed;
            }
            else if (InputMode == InputMode.Capture)
            {
                // 在捕获模式且非捕获状态下，显示在HotkeyDisplayText上
                ButtonText = "";
                HotkeyDisplayText = displayText;
                ShowPlaceholder = Visibility.Collapsed;
                HideText = Visibility.Visible;
            }
            else
            {
                // 在下拉模式下，只显示背景文本，避免重影
                ButtonText = "";
                HotkeyDisplayText = displayText;
                ShowPlaceholder = Visibility.Collapsed;
                HideText = Visibility.Visible;
            }
        }

        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (!_isCapturing && (HotkeyKeys.Count == 0))
        {
            ButtonText = "KeyMappingDialog_Placeholder_ClickToSet".GetLocalized() ?? "点击设置";
        }
    }

    private void UpdateModeDisplay()
    {
        try
        {
            if (InputMode == InputMode.Dropdown)
            {
                CaptureButtonVisibility = Visibility.Collapsed;
                DropdownVisibility = Visibility.Visible;

                // 确保下拉列表已正确初始化
                EnsureDropdownInitialized();

                // 更新下拉选择，确保在有按键时正确显示
                UpdateDropdownSelection();

                // 根据是否有按键来设置显示文本
                if (HotkeyKeys.Count > 0)
                {
                    ShowPlaceholder = Visibility.Collapsed;
                    HideText = Visibility.Visible;
                    ButtonText = "";

                    // 更新显示，确保背景文本和下拉框文本都正确
                    UpdateDisplay();
                }
                else
                {
                    ShowPlaceholder = Visibility.Visible;
                    HideText = Visibility.Collapsed;
                    ButtonText = "";
                }
            }
            else
            {
                CaptureButtonVisibility = Visibility.Visible;
                DropdownVisibility = Visibility.Collapsed;

                // 更新显示，确保在有按键时正确显示
                UpdateDisplay();
            }
        }
        catch (Exception ex)
        {
            // 记录异常但不中断程序
            System.Diagnostics.Debug.WriteLine($"UpdateModeDisplay error: {ex.Message}");
            // 如果出错，默认回到捕获模式
            CaptureButtonVisibility = Visibility.Visible;
            DropdownVisibility = Visibility.Collapsed;
        }
    }

    private static bool IsModifierKey(VirtualKey key)
    {
        return key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
                    or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
                    or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
                    or VirtualKey.LeftWindows or VirtualKey.RightWindows;
    }

    private static int GetKeyOrder(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.LeftWindows or VirtualKey.RightWindows => 1,
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => 2,
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => 3,
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => 4,
            _ => 5
        };
    }

    private static string GetKeyDisplayName(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.LeftWindows or VirtualKey.RightWindows => "Win",
            VirtualKey.Control => Localized("KeyMapping_Key_AnyCtrl", "Ctrl（任意）"),
            VirtualKey.LeftControl => Localized("KeyMapping_Key_LeftCtrl", "左 Ctrl"),
            VirtualKey.RightControl => Localized("KeyMapping_Key_RightCtrl", "右 Ctrl"),
            VirtualKey.Menu => Localized("KeyMapping_Key_AnyAlt", "Alt（任意）"),
            VirtualKey.LeftMenu => Localized("KeyMapping_Key_LeftAlt", "左 Alt"),
            VirtualKey.RightMenu => Localized("KeyMapping_Key_RightAlt", "右 Alt"),
            VirtualKey.Shift => Localized("KeyMapping_Key_AnyShift", "Shift（任意）"),
            VirtualKey.LeftShift => Localized("KeyMapping_Key_LeftShift", "左 Shift"),
            VirtualKey.RightShift => Localized("KeyMapping_Key_RightShift", "右 Shift"),
            VirtualKey.Space => Localized("KeyMapping_Key_Space", "空格键"),
            VirtualKey.Tab => "Tab",
            VirtualKey.Enter => "Enter",
            VirtualKey.Escape => "Esc",
            VirtualKey.Back => "Backspace",
            VirtualKey.Delete => "Delete",
            VirtualKey.Insert => "Insert",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "Page Up",
            VirtualKey.PageDown => "Page Down",
            VirtualKey.Up => "↑",
            VirtualKey.Down => "↓",
            VirtualKey.Left => "←",
            VirtualKey.Right => "→",
            VirtualKey.Clear => Localized("KeyMapping_Key_Clear", "清除键"),
            VirtualKey.CapitalLock => "Caps Lock",
            VirtualKey.NumberKeyLock => "Num Lock",
            VirtualKey.Scroll => "Scroll Lock",
            VirtualKey.Snapshot => "Print Screen",
            VirtualKey.Pause => "Pause",
            VirtualKey.Application => Localized("KeyMapping_Key_Menu", "菜单键"),
            VirtualKey.Sleep => Localized("KeyMapping_Key_Sleep", "休眠键"),
            VirtualKey.Number0 => "0",
            VirtualKey.Number1 => "1",
            VirtualKey.Number2 => "2",
            VirtualKey.Number3 => "3",
            VirtualKey.Number4 => "4",
            VirtualKey.Number5 => "5",
            VirtualKey.Number6 => "6",
            VirtualKey.Number7 => "7",
            VirtualKey.Number8 => "8",
            VirtualKey.Number9 => "9",
            >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9 =>
                LocalizedFormat("KeyMapping_Key_NumberPad_Format", "小键盘 {0}", (int)key - (int)VirtualKey.NumberPad0),
            VirtualKey.Multiply => LocalizedFormat("KeyMapping_Key_NumberPad_Format", "小键盘 {0}", "*"),
            VirtualKey.Add => LocalizedFormat("KeyMapping_Key_NumberPad_Format", "小键盘 {0}", "+"),
            VirtualKey.Separator => Localized("KeyMapping_Key_NumberPadSeparator", "小键盘分隔符"),
            VirtualKey.Subtract => LocalizedFormat("KeyMapping_Key_NumberPad_Format", "小键盘 {0}", "-"),
            VirtualKey.Decimal => LocalizedFormat("KeyMapping_Key_NumberPad_Format", "小键盘 {0}", "."),
            VirtualKey.Divide => LocalizedFormat("KeyMapping_Key_NumberPad_Format", "小键盘 {0}", "/"),
            >= VirtualKey.F1 and <= VirtualKey.F24 => $"F{(int)key - (int)VirtualKey.F1 + 1}",
            >= VirtualKey.A and <= VirtualKey.Z => ((char)key).ToString(),
            _ => GetFallbackKeyDisplayName(key)
        };
    }

    private static string GetFallbackKeyDisplayName(VirtualKey key)
    {
        string? oemName = (int)key switch
        {
            0xBA => "; / :",
            0xBB => "= / +",
            0xBC => ", / <",
            0xBD => "- / _",
            0xBE => ". / >",
            0xBF => "/ / ?",
            0xC0 => "` / ~",
            0xDB => "[ / {",
            0xDC => "\\ / |",
            0xDD => "] / }",
            0xDE => "' / \"",
            0xE2 => "\\ / |",
            _ => null,
        };
        if (oemName is not null) return oemName;

        string enumName = key.ToString();
        return int.TryParse(enumName, out _)
            ? LocalizedFormat("KeyMapping_Key_Unknown_Format", "按键 {0}", (int)key)
            : enumName;
    }

    private static string Localized(string resourceKey, string fallback)
    {
        string localized = resourceKey.GetLocalized();
        return string.IsNullOrWhiteSpace(localized) || localized == resourceKey ? fallback : localized;
    }

    private static string LocalizedFormat(string resourceKey, string fallback, object value)
    {
        string format = Localized(resourceKey, fallback);
        try
        {
            return string.Format(format, value);
        }
        catch (FormatException)
        {
            return string.Format(fallback, value);
        }
    }

    public event EventHandler? HotkeyChanged;

    private void OnHotkeyChanged()
    {
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    // 获取下拉选项
    public List<HotkeyOption> DropdownOptions => GetDropdownOptions();

    private List<HotkeyOption> GetDropdownOptions()
    {
        var options = new List<HotkeyOption>();
        void AddKeyboard(VirtualKey key) => options.Add(new HotkeyOption(GetKeyDisplayName(key), (int)key));

        // 修饰键
        AddKeyboard(VirtualKey.LeftWindows);
        AddKeyboard(VirtualKey.Control);
        AddKeyboard(VirtualKey.LeftControl);
        AddKeyboard(VirtualKey.RightControl);
        AddKeyboard(VirtualKey.Menu);
        AddKeyboard(VirtualKey.LeftMenu);
        AddKeyboard(VirtualKey.RightMenu);
        AddKeyboard(VirtualKey.Shift);
        AddKeyboard(VirtualKey.LeftShift);
        AddKeyboard(VirtualKey.RightShift);

        // 功能键
        for (var i = 1; i <= 12; i++) // 只到F12，更常见的功能键
        {
            var key = (VirtualKey)(111 + i); // F1-F12
            if (Enum.IsDefined(typeof(VirtualKey), key))
            {
                AddKeyboard(key);
            }
        }

        // 数字键（主键盘）- 优先级最高
        for (var i = 0; i < 10; i++)
        {
            var key = (VirtualKey)('0' + i);
            AddKeyboard(key);
        }

        // 字母键
        for (var i = 0; i < 26; i++)
        {
            var key = (VirtualKey)('A' + i);
            AddKeyboard(key);
        }

        // 特殊键
        foreach (VirtualKey key in new[]
                 {
                     VirtualKey.Space, VirtualKey.Tab, VirtualKey.Enter, VirtualKey.Escape,
                     VirtualKey.Back, VirtualKey.Delete, VirtualKey.Insert, VirtualKey.Home,
                     VirtualKey.End, VirtualKey.PageUp, VirtualKey.PageDown, VirtualKey.Up,
                     VirtualKey.Down, VirtualKey.Left, VirtualKey.Right, VirtualKey.Clear,
                     VirtualKey.CapitalLock, VirtualKey.NumberKeyLock, VirtualKey.Scroll,
                     VirtualKey.Snapshot, VirtualKey.Pause, VirtualKey.Application, VirtualKey.Sleep,
                 })
        {
            AddKeyboard(key);
        }

        // 数字小键盘 - 优先级较低
        for (var i = 0; i < 10; i++) AddKeyboard((VirtualKey)((int)VirtualKey.NumberPad0 + i));
        AddKeyboard(VirtualKey.Multiply);
        AddKeyboard(VirtualKey.Add);
        AddKeyboard(VirtualKey.Separator);
        AddKeyboard(VirtualKey.Subtract);
        AddKeyboard(VirtualKey.Decimal);
        AddKeyboard(VirtualKey.Divide);

        // 主键盘标点符号（WinRT VirtualKey 未为这些 OEM 键提供名称）。
        foreach (int keyCode in new[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xE2 })
            AddKeyboard((VirtualKey)keyCode);

        // 鼠标按键
        for (int mouseCode = 1; mouseCode <= 7; mouseCode++)
            options.Add(new HotkeyOption(GetMouseDisplayName(mouseCode), mouseCode));

        return options;
    }

    private void OnDropdownSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is HotkeyOption selectedOption)
            {
                // 确保选择的选项有效且不是初始化过程
                if (selectedOption.KeyCode >= 0 && InputMode == InputMode.Dropdown) // 有效按键码且在下拉模式下
                {
                    // 防止重复设置相同值
                    if (HotkeyKeys.Count == 0 || HotkeyKeys[0] != selectedOption.KeyCode)
                    {
                        HotkeyKeys = new List<int> { selectedOption.KeyCode };
                        OnHotkeyChanged();
                        // 不需要手动调用UpdateDisplay，因为OnHotkeyKeysChanged会处理显示更新
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 记录异常但不中断程序
            System.Diagnostics.Debug.WriteLine($"OnDropdownSelectionChanged error: {ex.Message}");
        }
    }

    private void UpdateDropdownSelection()
    {
        try
        {
            if (KeyDropdown != null && HotkeyKeys.Count > 0)
            {
                var firstKey = HotkeyKeys[0];
                var matchingOption = DropdownOptions.FirstOrDefault(opt => opt.KeyCode == firstKey);

                // 使用异步方式设置选择，确保UI已完全加载
                KeyDropdown.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // 临时移除事件处理器以防止触发不必要的事件
                        KeyDropdown.SelectionChanged -= OnDropdownSelectionChanged;

                        if (matchingOption != null)
                        {
                            // 确保在下拉列表中找到匹配项后再设置选择
                            var itemIndex = DropdownOptions.IndexOf(matchingOption);
                            if (itemIndex >= 0 && itemIndex < KeyDropdown.Items.Count)
                            {
                                KeyDropdown.SelectedIndex = itemIndex;
                            }
                            else
                            {
                                KeyDropdown.SelectedIndex = -1;
                            }
                        }
                        else
                        {
                            // 如果没有找到匹配项，清除选择
                            KeyDropdown.SelectedIndex = -1;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"UpdateDropdownSelection (inner) error: {ex.Message}");
                    }
                    finally
                    {
                        // 重新添加事件处理器
                        KeyDropdown.SelectionChanged += OnDropdownSelectionChanged;
                    }
                });
            }
            else if (KeyDropdown != null)
            {
                // 如果没有按键，清除选择
                KeyDropdown.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        KeyDropdown.SelectionChanged -= OnDropdownSelectionChanged;
                        KeyDropdown.SelectedIndex = -1;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"UpdateDropdownSelection (empty) error: {ex.Message}");
                    }
                    finally
                    {
                        KeyDropdown.SelectionChanged += OnDropdownSelectionChanged;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // 记录异常但不中断程序
            System.Diagnostics.Debug.WriteLine($"UpdateDropdownSelection error: {ex.Message}");
        }
    }

    private string GetDisplayNameForKeyCode(int keyCode)
    {
        // 首先检查是否是鼠标按键
        if (IsMouseKeyCode(keyCode))
        {
            return GetMouseDisplayName(keyCode);
        }

        return GetKeyDisplayName((VirtualKey)keyCode);
    }

    private void EnsureDropdownInitialized()
    {
        try
        {
            // 确保下拉列表已正确初始化
            if (KeyDropdown != null && DropdownOptions.Count > 0)
            {
                // 强制刷新下拉列表项
                KeyDropdown.ItemsSource = null;
                KeyDropdown.ItemsSource = DropdownOptions;

                // 确保没有默认选择
                KeyDropdown.SelectedIndex = -1;

                // 如果有现有按键，更新选择
                UpdateDropdownSelection();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EnsureDropdownInitialized error: {ex.Message}");
        }
    }

    
    
    // 辅助方法
    private static bool IsMouseKeyCode(int keyCode) => keyCode switch
    {
        >= 1 and <= 7 => true, // 鼠标按键（包括滚轮上下6,7）
        _ => false
    };

    private static InputMode? FindParentInputMode(DependencyObject child)
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            // 使用反射查找 InputMode 属性
            var inputModeProp = parent.GetType().GetProperty("InputMode");
            if (inputModeProp?.PropertyType == typeof(InputMode))
            {
                return (InputMode?)inputModeProp.GetValue(parent);
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private static string GetMouseDisplayName(int mouseCode) => mouseCode switch
    {
        1 => Localized("KeyMapping_Mouse_Left", "鼠标左键"),
        2 => Localized("KeyMapping_Mouse_Right", "鼠标右键"),
        3 => Localized("KeyMapping_Mouse_Middle", "鼠标中键"),
        4 => Localized("KeyMapping_Mouse_X1", "鼠标侧键 1"),
        5 => Localized("KeyMapping_Mouse_X2", "鼠标侧键 2"),
        6 => Localized("KeyMapping_Mouse_WheelUp", "滚轮向上"),
        7 => Localized("KeyMapping_Mouse_WheelDown", "滚轮向下"),
        _ => LocalizedFormat("KeyMapping_Mouse_Unknown_Format", "鼠标按键 {0}", mouseCode),
    };
}

public class HotkeyOption
{
    public string DisplayName { get; set; }
    public int KeyCode { get; set; }
    public bool IsSeparator { get; set; }

    public HotkeyOption(string displayName, int keyCode, bool isSeparator = false)
    {
        DisplayName = displayName;
        KeyCode = keyCode;
        IsSeparator = isSeparator;
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
