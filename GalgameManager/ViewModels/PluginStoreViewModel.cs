using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class PluginStoreViewModel(
    IInfoService infoService,
    IBgTaskService bgTaskService,
    IPluginService pluginService,
    ILocalSettingsService settingsService)
    : ObservableRecipient, INavigationAware
{
    public AdvancedCollectionView Plugins { get; } = new();
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly ObservableCollection<PluginTypeViewModel> PluginTypes = [];
    [ObservableProperty] private PluginTypeViewModel _selectedPluginType = null!;
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly List<StorePluginFilter> StatusFilters = StorePluginFilterHelper.GetAllFilters();
    [ObservableProperty] private StorePluginFilter _selectedStatusFilter = StorePluginFilter.All;
    /// 过滤结果为空时才提示"没有插件"，插件还在下载的时候空列表是正常的
    [ObservableProperty] private bool _showEmptyHint;
    private bool _loading = true;

    public void OnNavigatedTo(object parameter)
    {
        Plugins.Filter = IsVisible; //过滤器只设置一次，条件变化时走RefreshFilter，原因见IsVisible的注释
        Plugins.VectorChanged += (_, _) => UpdateEmptyHint(); //新增插件与RefreshFilter都会走到这里
        foreach (PluginType type in PluginTypeHelper.GetAllTypes())
            PluginTypes.Add(new()
            {
                Type = type, Title = type.GetLocalized(), Icon = new FontIcon { Glyph = type.ToGlyph() }
            });
        _ = LoadPluginsAsync();
        SelectedPluginType = PluginTypes.FirstOrDefault(p => p.Type == PluginType.All) ?? PluginTypes.First();
    }

    private async Task LoadPluginsAsync()
    {
        try
        {
            await bgTaskService.AddBgTask(new GetStorePluginTask(Plugins));
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
        finally
        {
            _loading = false;
            UpdateEmptyHint();
        }
    }

    private void UpdateEmptyHint() => ShowEmptyHint = !_loading && Plugins.Count == 0;

    public void OnNavigatedFrom() {}

    [RelayCommand]
    private async Task ItemClickAsync(StorePlugin? clickedItem)
    {
        try
        {
            if (clickedItem == null) return;
            // infoService.Info(InfoBarSeverity.Success, clickedItem.Name);
            StorePluginDialog dialog = new(clickedItem, pluginService.PluginOffloadInProgress);
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                StorePluginVersion version = dialog.SelectedVersion;
                if ((await pluginService.GetAllPluginsAsync()).Any(p => p.Id == clickedItem.Id))
                {
                    List<ToInstallStorePlugin> list = await settingsService.ReadSettingAsync<List<ToInstallStorePlugin>>
                                                          (KeyValues.ToUpgradePlugin) ?? [];
                    list.Add(new ToInstallStorePlugin{Plugin = clickedItem, Version = version});
                    await settingsService.SaveSettingAsync(KeyValues.ToUpgradePlugin, list);
                    infoService.Info(InfoBarSeverity.Success, "PluginStorePage_UpgradeQueued".GetLocalized());
                    return;
                }
                await bgTaskService.AddBgTask(new InstallStorePluginTask(clickedItem, version));
                // 安装任务会回写clickedItem的状态，但ACV不会因为元素属性变化自动重新过滤，这里手动刷一次
                Plugins.RefreshFilter();
            }
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
    }

    /// 把过滤条件恢复成默认值（全部类别 + 全部安装状态），给空状态提示用
    [RelayCommand]
    private void ResetFilter()
    {
        SelectedStatusFilter = StorePluginFilter.All;
        SelectedPluginType = PluginTypes.FirstOrDefault(p => p.Type == PluginType.All) ?? SelectedPluginType;
    }

    partial void OnSelectedPluginTypeChanged(PluginTypeViewModel value) => Plugins.RefreshFilter();

    partial void OnSelectedStatusFilterChanged(StorePluginFilter value) => Plugins.RefreshFilter();

    /// <summary>
    /// 插件列表的过滤条件（类别 + 安装状态），直接读取当前选中项。<br/>
    /// 注意：不要在条件变化时重新给<see cref="AdvancedCollectionView.Filter"/>赋值。这个方法只捕获this，
    /// 每次生成的委托在Delegate相等性上（比较Method+Target）都是相等的，
    /// 而Filter的setter有 <c>if (_filter == value) return;</c> 短路，重新赋值不会触发刷新。
    /// 条件变化时一律调用<see cref="AdvancedCollectionView.RefreshFilter"/>。
    /// </summary>
    private bool IsVisible(object p)
    {
        StorePlugin plugin = (StorePlugin)p;
        PluginType type = SelectedPluginType?.Type ?? PluginType.All;
        if (type != PluginType.All && !plugin.Types.Contains(type)) return false;
        return SelectedStatusFilter switch
        {
            // 有更新的插件同样是已安装的插件
            StorePluginFilter.Installed => plugin.Status is StorePluginStatus.Installed
                or StorePluginStatus.UpdateAvailable,
            StorePluginFilter.UpdateAvailable => plugin.Status == StorePluginStatus.UpdateAvailable,
            _ => true,
        };
    }
}

public class PluginTypeViewModel
{
    public string Title = string.Empty;
    public IconElement Icon = new FontIcon() { Glyph = "\uE8EF" };
    public required PluginType Type { get; init; }
}
