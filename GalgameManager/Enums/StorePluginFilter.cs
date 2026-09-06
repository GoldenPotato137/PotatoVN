namespace GalgameManager.Enums;

// 注意：本Enum的每一个元素都需要添加Localize: StorePluginFilter_xxx 的本地化翻译
/// <summary>
/// 插件商店里按安装状态过滤插件的选项
/// </summary>
public enum StorePluginFilter
{
    All,                //全部
    Installed,          //已安装（含有更新的）
    UpdateAvailable,    //有更新
}

public static class StorePluginFilterHelper
{
    public static List<StorePluginFilter> GetAllFilters() =>
        Enum.GetValues(typeof(StorePluginFilter)).Cast<StorePluginFilter>().ToList();
}
