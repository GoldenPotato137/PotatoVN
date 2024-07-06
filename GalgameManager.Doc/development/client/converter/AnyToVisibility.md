
# AnyToVisibilityConverter

当值不为null时，将值转换为Visibility.Visible，否则转换为Visibility.Collapsed。

```
例：ChangeSourceDialog.xaml
<InfoBar Severity="Error" IsOpen="True" Message="{x:Bind WarningText, Mode=OneWay}"
         Visibility="{x:Bind WarningText, Mode=OneWay, Converter={StaticResource AnyToVisibility}}"/>
...
<converter:AnyToVisibilityConverter x:Key="AnyToVisibility" />
```