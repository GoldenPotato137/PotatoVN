namespace GalgameManager.Models;

/// <summary>
/// 随数据导出/导入一起携带的“页面设置”，目前包含游戏列表页（主页）与库页的排序相关设置，以及背景图设置。<br/>
/// 所有字段可空：为 null 表示导出包中不含该项，导入时不覆盖本机现有设置。<br/>
/// 枚举值统一以 int 存储，与各 ViewModel 读写设置时的格式保持一致。
/// </summary>
public class PageSettings
{
    // 游戏列表页（主页）
    public int? PrimarySortKey;
    public bool? PrimarySortDescending;
    public int? SecondarySortKey;
    public bool? SecondarySortDescending;
    /// 主页手动排序时记录的游戏 Uuid 顺序
    public List<string>? CustomSortOrder;

    // 库页
    public int? LibrarySortKey;
    public bool? LibraryGameSortDescending;
    public int? LibraryFolderSortKey;
    public bool? LibraryFolderSortDescending;

    // 背景图，图片本身以Background文件夹的形式打包在导出包里
    public bool? CustomBackgroundEnabled;
    public bool? UseBannerAsBackground;
    public int? BackgroundStretchMode;
}
