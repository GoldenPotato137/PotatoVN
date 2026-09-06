# 自定义控件介绍

本文档介绍了应用中使用的各种自定义控件，包括其功能、属性和使用方法。

---

## Setting

一个左侧有上下两行描述（一行Title，一行Description），右侧允许放置一个自定义控件的设置项控件。

**控件属性：**

* `Title` (string): 设置的标题。
* `Description` (string): 设置的描述。
* `Content` (UIElement): 放置在右侧的自定义控件。

**使用样例：**
请注意，样例中的 `x:Uid` 用于国际化，会自动设置 `Title` 属性。`Content` 区域被放置了一个 `NumberBox`。

```xml
<control:Setting x:Uid="SettingsPage_Other_AutoExport_Interval"  
                 Description="{x:Bind ViewModel.AutoExportIntervalDescription, Mode=OneWay}"  
                 Visibility="{x:Bind ViewModel.IsAutoExportEnabled, Mode=OneWay}">  
    <NumberBox Value="{x:Bind ViewModel.AutoExportInterval, Mode=TwoWay}"  
               SpinButtonPlacementMode="Compact"  
               SmallChange="1" LargeChange="24" Minimum="1"  
               Width="100"/>  
</control:Setting>
```

---

## SettingToggleSwitch

一个左侧有标题和描述，右侧固定为一个 `ToggleSwitch` 的快捷设置项控件。

**控件属性：**

* `Title` (string): 设置的标题。
* `Description` (string): 设置的描述。
* `IsOn` (bool): 绑定到右侧 `ToggleSwitch` 的布尔值。

**使用样例：**
此控件用于简单的布尔值开关设置。

```xml
<views:SettingToggleSwitch x:Uid="SettingsPage_Theme"
                             Description="切换应用的主题"
                             IsOn="{x:Bind ViewModel.IsLightThemeEnabled, Mode=TwoWay}" />
```

---

## ComboBoxWithI18N

一个内置了国际化支持的 `ComboBox` 控件。当 `ItemsSource` 为枚举类型时，它会自动使用 `EnumToStringConverter` 来显示本地化的字符串。

**控件属性：**

* `ItemsSource` (object): 控件的数据源。
* `SelectedItem` (object): 当前选中的项。
* `ItemTemplate` (DataTemplate): 可选，用于自定义下拉列表中项目的显示模板。

**控件事件：**

* `SelectedItemChangedEvent`: 当选中项发生变化时触发。

**使用样例：**
该控件简化了在 `ComboBox` 中展示枚举类型或其他需要本地化显示的数据的流程。

```xml
<control:ComboBoxWithI18N ItemsSource="{x:Bind ViewModel.AvailableSortOptions}"
                          SelectedItem="{x:Bind ViewModel.CurrentSortOption, Mode=TwoWay}" />
```

---

## Panel

一个带有预设样式的容器控件，提供圆角、背景色和边框，用于包裹其他UI元素，使其风格统一。

**控件属性：**

* `Content` (UIElement): 放置在容器内部的UI元素。

**使用样例：**
`Panel` 内可以包含任意UI元素，通常用于将一组相关的控件组织在一起。

```xml
<control:Panel>
    <StackPanel Spacing="10">
        <TextBlock Text="这是一个在Panel内部的标题" Style="{ThemeResource SubtitleTextBlockStyle}"/>
        <Button Content="确认"/>
    </StackPanel>
</control:Panel>
```

---

## 已知问题：ItemsRepeater + WrapLayout 滚动后重叠

CommunityToolkit 的 `WrapLayout` 有已知 bug（[CommunityToolkit/Windows#707](https://github.com/CommunityToolkit/Windows/issues/707)）：当 `ItemsRepeater` 滚出屏幕后更新其数据源，元素会堆叠在 (0,0)，滚动回来时表现为多行内容重叠。对集合做 `InvalidateMeasure()` / `UpdateLayout()` 不能可靠修复。

**结论：** 位于 `ScrollViewer` 内、且本身不享受虚拟化的小列表（如详情页头部字段、几十项以内），不要使用 `ItemsRepeater` + `WrapLayout`，改用 `ItemsControl` + `WrapPanel`（同一命名空间 `CommunityToolkit.WinUI.Controls`，无新依赖）：

```xml
<ItemsControl ItemsSource="{x:Bind Items}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <cmtkControls:WrapPanel Orientation="Horizontal" HorizontalSpacing="5" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```

`GameHeaderPanel.xaml` 已按此方案修复。仓库中其余 `WrapLayout` 使用点（`GameTagPanel.xaml`、`GalgameSourcePage.xaml`、`CategorySettingPage.xaml`、`Views/Control/ObservableList.xaml`）如遇同类症状可同样替换。

**注意：** 模板根元素上的 `DataContext="{x:Bind}"` 在 `ItemsRepeater` 下无害（其管线会调用 `ProcessBindings` 移除内部 `DataContextChanged` 监听），但在 `ItemsControl`/`ContentPresenter` 下会形成"设置 DataContext → 触发 DataContextChanged → 绑定刷新 → 再次设置 DataContext"的无限递归，直接栈溢出。把 `ItemsRepeater` 模板改造成 `ItemsControl` 时必须删掉这个冗余属性——`ItemsControl` 会自动把数据项设为模板根的 DataContext，事件处理器里读 `button.DataContext` 不受影响。
