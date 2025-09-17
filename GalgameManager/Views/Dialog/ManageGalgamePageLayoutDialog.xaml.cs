using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views.Dialog;

public sealed partial class ManageGalgamePageLayoutDialog : ContentDialog
{
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();

    // 定义布局更改事件
    public static event EventHandler<bool>? LayoutChanged;

    public DisplayName[] PrimaryTitleTypes { get; } = {DisplayName.ChineseName, DisplayName.OriginalName, DisplayName.Name};
    public DisplayName[] SecondaryTitleTypes { get; } = {DisplayName.ChineseName, DisplayName.OriginalName, DisplayName.Name, DisplayName.None};
    public DisplayName GalgamePagePrimaryTitleType { get; set; } = DisplayName.ChineseName;
    public DisplayName GalgamePageSecondaryTitleType { get; set; } = DisplayName.OriginalName;
    
    public bool GalgamePageNewLayout { get; set; }
    public bool GalgamePageNewLayout_ShowPainter { get; set; }
    public bool GalgamePageNewLayout_ShowSeiyu { get; set; }
    public bool GalgamePageNewLayout_ShowWriter { get; set; }
    public bool GalgamePageNewLayout_ShowMusician { get; set; }
    public bool GalgamePageNewLayout_ShowBackground { get; set; }
    public bool GalgamePageNewLayout_ShowCover { get; set; }
    public bool GalgamePageNewLayout_ShowCoverWhenNoBackground { get; set; }
    public bool GalgamePageNewLayout_ShowExpectedPlayTime { get; set; }
    public bool GalgamePageNewLayout_ShowRating { get; set; }
    public bool GalgamePageNewLayout_ShowTags { get; set; }
    public bool GalgamePageNewLayout_ShowCharacters { get; set; }

    // 记录初始值，仅保存发生变化的键
    private DisplayName _initialPrimaryTitleType;
    private DisplayName _initialSecondaryTitleType;
    private bool _initialNewLayout;
    private bool _initialShowPainter;
    private bool _initialShowSeiyu;
    private bool _initialShowWriter;
    private bool _initialShowMusician;
    private bool _initialShowBackground;
    private bool _initialShowCover;
    private bool _initialShowCoverWhenNoBackground;
    private bool _initialShowExpectedPlayTime;
    private bool _initialShowRating;
    private bool _initialShowTags;
    private bool _initialShowCharacters;

    public ManageGalgamePageLayoutDialog()
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "ManageGalgamePageLayoutDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        
        // 明确设置宽度属性以覆盖默认样式限制
        MinWidth = 600;
        Width = 600;

        LoadSettings();
        PrimaryButtonClick += ManageGalgamePageLayoutDialog_PrimaryButtonClick;
        
        // 监听主标题和副标题的选择变化，确保它们不同
        PrimaryTitleComboBox.SelectionChanged += TitleComboBox_SelectionChanged;
        SecondaryTitleComboBox.SelectionChanged += TitleComboBox_SelectionChanged;
    }

    private void TitleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 检查两个ComboBox的SelectedItem是否为null，如果有一个为null则直接返回
        if (PrimaryTitleComboBox.SelectedItem == null || SecondaryTitleComboBox.SelectedItem == null)
        {
            return;
        }
        
        // 获取当前选择的DisplayName枚举值
        DisplayName primaryType = (DisplayName)PrimaryTitleComboBox.SelectedItem;
        DisplayName secondaryType = (DisplayName)SecondaryTitleComboBox.SelectedItem;

        // 只有当两个下拉框都选择了相同值（且不是"无"）时才需要处理冲突
        if (primaryType == secondaryType && secondaryType != DisplayName.None)
        {
            // 如果是副标题被改变，则将副标题设为不同于主标题的值
            if (ReferenceEquals(sender, SecondaryTitleComboBox))
            {
                // 找到一个不同的值
                DisplayName newValue = PrimaryTitleTypes.FirstOrDefault(t => t != primaryType && t != DisplayName.None);
                if (newValue != DisplayName.None)
                {
                    SecondaryTitleComboBox.SelectedItem = newValue;
                }
                else
                {
                    SecondaryTitleComboBox.SelectedItem = DisplayName.None;
                }
            }
            // 如果是主标题被改变，则将副标题设为不同的值
            else if (ReferenceEquals(sender, PrimaryTitleComboBox))
            {
                // 找到一个不同的值
                DisplayName newValue = SecondaryTitleTypes.FirstOrDefault(t => t != primaryType && t != DisplayName.None);
                if (newValue != DisplayName.None)
                {
                    SecondaryTitleComboBox.SelectedItem = newValue;
                }
                else
                {
                    SecondaryTitleComboBox.SelectedItem = DisplayName.None;
                }
            }
        }
    }

    private async void LoadSettings()
    {
        // 直接读取DisplayName枚举值
        GalgamePagePrimaryTitleType = await _localSettingsService.ReadSettingAsync<DisplayName>(KeyValues.GalgamePagePrimaryTitleType);
        _initialPrimaryTitleType = GalgamePagePrimaryTitleType;
        GalgamePageSecondaryTitleType = await _localSettingsService.ReadSettingAsync<DisplayName>(KeyValues.GalgamePageSecondaryTitleType);
        _initialSecondaryTitleType = GalgamePageSecondaryTitleType;
        
        GalgamePageNewLayout = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout);
        _initialNewLayout = GalgamePageNewLayout;
        GalgamePageNewLayout_ShowPainter = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowPainter);
        _initialShowPainter = GalgamePageNewLayout_ShowPainter;
        GalgamePageNewLayout_ShowSeiyu = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowSeiyu);
        _initialShowSeiyu = GalgamePageNewLayout_ShowSeiyu;
        GalgamePageNewLayout_ShowWriter = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowWriter);
        _initialShowWriter = GalgamePageNewLayout_ShowWriter;
        GalgamePageNewLayout_ShowMusician = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowMusician);
        _initialShowMusician = GalgamePageNewLayout_ShowMusician;
        GalgamePageNewLayout_ShowBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage);
        _initialShowBackground = GalgamePageNewLayout_ShowBackground;
        GalgamePageNewLayout_ShowCover = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_CoverImage);
        _initialShowCover = GalgamePageNewLayout_ShowCover;
        GalgamePageNewLayout_ShowCoverWhenNoBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground);
        _initialShowCoverWhenNoBackground = GalgamePageNewLayout_ShowCoverWhenNoBackground;
        GalgamePageNewLayout_ShowExpectedPlayTime = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime);
        _initialShowExpectedPlayTime = GalgamePageNewLayout_ShowExpectedPlayTime;
        GalgamePageNewLayout_ShowRating = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowRating);
        _initialShowRating = GalgamePageNewLayout_ShowRating;
        GalgamePageNewLayout_ShowTags = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowTags);
        _initialShowTags = GalgamePageNewLayout_ShowTags;
        GalgamePageNewLayout_ShowCharacters = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCharacters);
        _initialShowCharacters = GalgamePageNewLayout_ShowCharacters;
    }

    private async void ManageGalgamePageLayoutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();

        // 仅保存发生变化的设置项
        if (_initialPrimaryTitleType != GalgamePagePrimaryTitleType)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePagePrimaryTitleType, GalgamePagePrimaryTitleType);
        if (_initialSecondaryTitleType != GalgamePageSecondaryTitleType)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageSecondaryTitleType, GalgamePageSecondaryTitleType);
        if (_initialNewLayout != GalgamePageNewLayout)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout, GalgamePageNewLayout);
        if (_initialShowPainter != GalgamePageNewLayout_ShowPainter)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowPainter, GalgamePageNewLayout_ShowPainter);
        if (_initialShowSeiyu != GalgamePageNewLayout_ShowSeiyu)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowSeiyu, GalgamePageNewLayout_ShowSeiyu);
        if (_initialShowWriter != GalgamePageNewLayout_ShowWriter)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowWriter, GalgamePageNewLayout_ShowWriter);
        if (_initialShowMusician != GalgamePageNewLayout_ShowMusician)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowMusician, GalgamePageNewLayout_ShowMusician);
        if (_initialShowBackground != GalgamePageNewLayout_ShowBackground)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowHeaderImage, GalgamePageNewLayout_ShowBackground);
        if (_initialShowCover != GalgamePageNewLayout_ShowCover)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_CoverImage, GalgamePageNewLayout_ShowCover);
        if (_initialShowCoverWhenNoBackground != GalgamePageNewLayout_ShowCoverWhenNoBackground)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground, GalgamePageNewLayout_ShowCoverWhenNoBackground);
        if (_initialShowExpectedPlayTime != GalgamePageNewLayout_ShowExpectedPlayTime)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime, GalgamePageNewLayout_ShowExpectedPlayTime);
        if (_initialShowRating != GalgamePageNewLayout_ShowRating)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowRating, GalgamePageNewLayout_ShowRating);
        if (_initialShowTags != GalgamePageNewLayout_ShowTags)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowTags, GalgamePageNewLayout_ShowTags);
        if (_initialShowCharacters != GalgamePageNewLayout_ShowCharacters)
            await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowCharacters, GalgamePageNewLayout_ShowCharacters);

        // 触发事件通知
        LayoutChanged?.Invoke(this, GalgamePageNewLayout);

        deferral.Complete();
    }
}

