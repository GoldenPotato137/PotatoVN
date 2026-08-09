using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.WinApp.Base.Contracts.NavigationApi;
using GalgameManager.WinApp.Base.Models.Filters;
using GalgameManager.WinApp.Base.Models.Plugin;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IPotatoVnApi
{
    //与游戏相关的API
    #region GAMES

    /// <summary>
    /// 获取所有游戏，这个列表只是一个快照（即后续添加的游戏或删除的游戏均不会在这个List中反馈）
    /// </summary>
    /// <returns></returns>
    public List<Galgame> GetAllGames();

    /// <summary>
    /// 按宿主生成的稳定 UUID 获取游戏。
    /// </summary>
    public Galgame? GetGameByUuid(Guid? uuid);

    /// <summary>
    /// 按跨信息源 UID 获取游戏。
    /// </summary>
    public Galgame? GetGameByUid(GalgameUid? uid, GalgameUidFetchMode mode = GalgameUidFetchMode.Same);

    /// <summary>
    /// 按指定信息源 ID 获取游戏。
    /// </summary>
    public Galgame? GetGameById(string? id, RssType rssType);

    /// <summary>
    /// 按名称获取游戏。
    /// </summary>
    public Galgame? GetGameByName(string? name);

    /// <summary>
    /// 以本地文件夹安装的方式添加游戏。识别到已有逻辑游戏时，会为其新增安装实例。
    /// </summary>
    /// <param name="path">游戏根目录的绝对路径。通常应传入包含主 exe 的文件夹路径，而不是 exe / bat 文件本身。</param>
    /// <param name="force">当信息源未搜到该游戏时是否仍强制加入库中</param>
    /// <param name="requireConfirm">是否弹出搜刮结果确认对话框。批量导入或后台任务通常建议传 <c>false</c>，否则插件会等待用户确认。</param>
    /// <returns>新增或关联安装实例后的逻辑游戏</returns>
    /// <remarks><b>注意捕获异常；</b>该函数可在非UI线程上调用</remarks>
    public Task<Galgame> AddGameInstallation(string path, bool force = true, bool requireConfirm = true);

    /// <summary>
    /// 获取指定逻辑游戏的全部本地安装实例只读快照。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <returns>本地安装实例快照</returns>
    public IReadOnlyList<GameInstallationInfo> GetGameInstallations(Galgame game);

    /// <summary>
    /// 启动逻辑游戏的指定安装实例。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="installationId">安装实例Id；为null时使用首选实例</param>
    public Task LaunchGameAsync(Galgame game, Guid? installationId = null);

    /// <summary>
    /// 获取指定安装实例的独立配置副本；找不到时返回 null。
    /// </summary>
    public LocalInstallationConfig? GetGameInstallationConfiguration(Galgame game, Guid installationId);

    /// <summary>
    /// 更新指定安装实例的本机配置并持久化。
    /// </summary>
    public Task UpdateGameInstallationAsync(Galgame game, Guid installationId,
        LocalInstallationConfig configuration, bool makePreferred = false);

    /// <summary>
    /// 将指定本地安装实例设为首选实例并持久化。
    /// </summary>
    public Task SetPreferredGameInstallationAsync(Galgame game, Guid installationId);

    /// <summary>
    /// 从游戏库解除指定安装实例；仅当 deleteFiles 为 true 时删除磁盘文件。
    /// </summary>
    public Task RemoveGameInstallationAsync(Galgame game, Guid installationId, bool deleteFiles = false);

    /// <summary>
    /// 添加一个虚拟游戏（非本地游戏）。
    /// </summary>
    /// <param name="name">虚拟游戏显示名。这里传的是游戏名，不是文件路径。</param>
    /// <param name="force">当信息源未搜到该游戏时是否仍强制加入库中。</param>
    /// <param name="requireConfirm">是否弹出搜刮结果确认对话框</param>
    /// <returns>返回最终加入列表中的 <see cref="Galgame"/>。</returns>
    /// <remarks>
    /// <b>注意捕获异常；</b>该函数可在非UI线程上调用 <br/>
    /// 若库中已存在同一游戏（例如同 UID / 同名占位条目），宿主通常会抛出“已在库中”异常，而不是重复创建。<br/>
    /// 创建纯占位条目的常见写法：<c>await hostApi.AddVirtualGame(name, requireConfirm: false)</c>。
    /// </remarks>
    public Task<Galgame> AddVirtualGame(string name, bool force = true, bool requireConfirm = true);

    /// <summary>
    /// 直接添加一个已经构造好的虚拟游戏，不触发搜刮流程。
    /// </summary>
    public Task AddVirtualGameAsync(Galgame game);

    /// <summary>
    /// 保存游戏及其本地元数据。
    /// </summary>
    public Task SaveGameAsync(Galgame game);

    /// <summary>
    /// 保存游戏的 meta 备份；sourceId 为 null 时保存到所有已启用备份的来源。
    /// </summary>
    public Task SaveGameMetadataAsync(Galgame game, Guid? sourceId = null);

    /// <summary>
    /// 从游戏库删除逻辑游戏；仅当 removeFromDisk 为 true 时删除磁盘文件。
    /// </summary>
    public Task RemoveGameAsync(Galgame game, bool removeFromDisk = false);

    /// <summary>
    /// 对库内游戏执行完整搜刮流程。
    /// </summary>
    public Task<Galgame> ParseGameAsync(Galgame game, RssType rssType = RssType.None,
        bool requireConfirm = false, GameParseType type = GameParseType.All);

    /// <summary>
    /// 只搜刮基本游戏信息，可用于尚未加入游戏库的对象。
    /// </summary>
    public Task<Galgame> ParseGameInfoOnlyAsync(Galgame game, RssType rssType = RssType.None,
        bool requireConfirm = false);

    /// <summary>
    /// 搜刮角色信息并直接更新传入对象。
    /// </summary>
    public Task<GalgameCharacter> ParseGameCharacterAsync(GalgameCharacter character,
        RssType rssType = RssType.None);

    /// <summary>
    /// 搜刮游戏封面或头图 URL。
    /// </summary>
    public Task<List<string>> ParseGameImagesAsync(Galgame game, GameParseType type);

    #endregion

    #region SOURCES

    /// <summary>
    /// 获取全部游戏来源快照。
    /// </summary>
    public List<GalgameSourceBase> GetAllSources();

    public GalgameSourceBase? GetSourceById(Guid sourceId);

    public GalgameSourceBase? GetSourceByUrl(string url);

    public GalgameSourceBase? GetSource(GalgameSourceType type, string path);

    /// <summary>
    /// 添加游戏来源，并可选择立即扫描。
    /// </summary>
    public Task<GalgameSourceBase> AddSourceAsync(GalgameSourceType type, string path,
        bool scan = true, bool manualSelectFolder = false);

    /// <summary>
    /// 删除游戏来源。宿主会显示现有的确认界面。
    /// </summary>
    public Task DeleteSourceAsync(GalgameSourceBase source);

    /// <summary>
    /// 不显示确认界面地删除游戏来源。
    /// </summary>
    /// <param name="source">要删除的来源</param>
    /// <param name="removeGames">是否同时删除失去最后一个来源的逻辑游戏；不会删除磁盘文件</param>
    public Task DeleteSourceAsync(GalgameSourceBase source, bool removeGames);

    public void ScanAllSources();

    public void ScanSource(GalgameSourceBase source);

    public void SaveSource(GalgameSourceBase source);

    /// <summary>
    /// 在不移动物理文件的情况下，把游戏关联到来源。
    /// </summary>
    public GameInstallationInfo? AddGameToSource(GalgameSourceBase source, Galgame game, string path,
        LocalInstallationConfig? localConfiguration = null);

    /// <summary>
    /// 启动物理移动后台任务。moveOutSourceId 为 null 时只执行移入。
    /// </summary>
    public BgTaskBase MoveGame(Galgame game, Guid? moveInSourceId, string? moveInPath = null,
        Guid? moveOutSourceId = null);

    /// <summary>
    /// 根据游戏路径计算其来源根路径。
    /// </summary>
    public string GetSourcePath(GalgameSourceType type, string gamePath);

    #endregion

    #region CATEGORIES

    public Task<List<CategoryGroup>> GetCategoryGroupsAsync();

    public CategoryGroup? GetCategoryGroup(Guid id);

    public Category? GetCategory(Guid id);

    public Category? GetCategory(string name);

    public Category? GetDeveloperCategory(Galgame game);

    public Category? GetEngineCategory(Galgame game);

    public CategoryGroup StatusCategoryGroup { get; }

    public CategoryGroup DeveloperCategoryGroup { get; }

    public CategoryGroup EngineCategoryGroup { get; }

    public CategoryGroup AddCategoryGroup(string name);

    public void DeleteCategoryGroup(CategoryGroup group);

    public void SaveCategory(Category category);

    public void SaveCategoryGroup(CategoryGroup group);

    public void DeleteCategory(Category category);

    public void AddCategoryToGroup(CategoryGroup group, Category category);

    public void RemoveCategoryFromGroup(CategoryGroup group, Category category);

    public void MergeCategories(Category target, Category source);

    /// <summary>
    /// 重新计算所有游戏的系统分类。
    /// </summary>
    public Task RefreshGameCategoriesAsync();

    /// <summary>
    /// 刷新分类的外部信息（目前为开发商图片）。
    /// </summary>
    public void RefreshCategory(Category category);

    #endregion

    // 与游戏列表界面过滤器相关的 API
    #region FILTERS

    /// <summary>
    /// 游戏列表界面（GameListPage）使用的过滤器。
    /// 这些 API 只影响“游戏列表”的筛选/显示结果，不会修改任何游戏数据。
    /// </summary>
    /// <remarks>
    /// 宿主会在 UI 线程执行这些操作；插件可以从任意线程调用。
    /// </remarks>
    void AddFilter(FilterBase filter);

    /// <summary>
    /// 从游戏列表界面移除一个过滤器（不存在则忽略）。
    /// </summary>
    void DeleteFilter(FilterBase filter);

    /// <summary>
    /// 清空游戏列表界面的过滤器（宿主会保留必要的内置过滤器，例如虚拟游戏开关相关过滤器）。
    /// </summary>
    void ClearFilters();

    /// <summary>
    /// 获取游戏列表界面当前启用的过滤器列表（快照）。
    /// </summary>
    /// <remarks>
    /// 返回的是当前列表的复制品，用于插件侧展示/判断；
    /// 若要修改过滤器，请使用 <see cref="AddFilter"/> / <see cref="DeleteFilter"/> / <see cref="ClearFilters"/>。
    /// </remarks>
    Task<List<FilterBase>> GetFiltersAsync();

    /// <summary>
    /// 判断游戏是否通过当前全部过滤器。
    /// </summary>
    bool ApplyFilters(Galgame game);

    /// <summary>
    /// 搜索可用过滤器。
    /// </summary>
    Task<List<FilterBase>> SearchFiltersAsync(string searchText);

    /// <summary>
    /// 用同类型过滤器替换当前过滤条件。
    /// </summary>
    void SetFilter(FilterBase filter);

    #endregion

    // 与 staff 相关的 API
    #region STAFF

    /// <summary>
    /// 根据 staffId 获取 staff，不存在则返回 null
    /// </summary>
    Staff? GetStaff(Guid? id);

    /// <summary>
    /// 按 Staff 标识符获取匹配度最高的 Staff。
    /// </summary>
    Staff? GetStaff(StaffIdentifier identifier);

    /// <summary>
    /// 获取所有 staff 列表（快照）
    /// </summary>
    List<Staff> GetStaffs();

    /// <summary>
    /// 获取某个游戏关联的 staff 列表（快照）
    /// </summary>
    List<Staff> GetStaffs(Galgame game);

    /// <summary>
    /// 保存 staff（新增 / 修改）。默认会触发同步逻辑（若宿主启用）。
    /// </summary>
    void SaveStaff(Staff staff, bool sync = true);

    /// <summary>
    /// 搜刮单个 Staff 信息。
    /// </summary>
    Task<Staff> ParseStaffAsync(Staff staff, RssType rssType);

    /// <summary>
    /// 搜刮并保存游戏关联的 Staff。
    /// </summary>
    Task ParseGameStaffAsync(Galgame game);

    /// <summary>
    /// 删除 Staff。
    /// </summary>
    void DeleteStaff(Staff staff, bool sync = true);

    #endregion

    //与插件数据存储相关的API
    #region DATA

    /// <summary>
    /// 读取本插件存储的数据
    /// </summary>
    /// <returns></returns>
    public Task<string?> GetDataAsync();

    /// <summary>
    /// 保存本插件存储的数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public Task SaveDataAsync(string data);

    #endregion

    //与消息相关的API
    #region MESSAGES

    /// <summary>
    /// 全局消息信使
    /// </summary>
    public IMessenger Messenger { get; }

    #endregion

    //与事件/通知相关的API
    #region NOTIFICATIONS

    /// <summary>
    /// 使用InfoBar通知信息，若title与msg均为空则关闭InfoBar
    /// </summary>
    /// <param name="infoBarSeverity"></param>
    /// <param name="title"></param>
    /// <param name="msg"></param>
    /// <param name="displayTimeMs"></param>
    public void Info(InfoBarSeverity infoBarSeverity, string? title = null, string? msg = null,int? displayTimeMs = 3000);

    /// <summary>
    /// 记录并通知事件
    /// </summary>
    /// <param name="infoBarSeverity">严重程度</param>
    /// <param name="title">事件名</param>
    /// <param name="exception">与之相关的异常，若不是异常则不填</param>
    /// <param name="msg">事件信息</param>
    /// <param name="callbackAction">点击按钮后执行的回调</param>
    /// <param name="callbackButtonText">按钮上显示的文字</param>
    public void Event(InfoBarSeverity infoBarSeverity, string title, Exception? exception = null, string? msg = null,
        Action? callbackAction = null, string? callbackButtonText = null);

    /// <summary>
    /// 不严重的非预期错误，仅在开发模式下通知
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="infoBarSeverity"></param>
    /// <param name="e"></param>
    public void DeveloperEvent(InfoBarSeverity infoBarSeverity = InfoBarSeverity.Warning, string? msg = null,
        Exception? e = null);

    /// <summary>
    /// 手动记录日志，默认只将severity >= InfoBarSeverity.Warning的日志通知，开发者模式下通知所有日志 <br/>
    /// <see cref="Event"/>会自动调用该方法记录日志
    /// </summary>
    /// <param name="severity"></param>
    /// <param name="msg"></param>
    public void Log(InfoBarSeverity severity = InfoBarSeverity.Warning, string msg = "");

    #endregion

    //与后台任务相关的API
    #region BG_TASKS

    /// <summary>
    /// 新增后台任务
    /// </summary>
    /// <returns>这个后台任务对应的task</returns>
    public Task AddBgTask(BgTaskBase bgTask);

    /// <summary>
    /// 获取所有后台任务
    /// </summary>
    public IEnumerable<BgTaskBase> GetBgTasks();

    /// <summary>
    /// 获取指定类型的后台任务，如果没有则返回null
    /// </summary>
    /// <param name="key">关键字</param>
    public T? GetBgTask<T>(string key) where T : BgTaskBase;

    #endregion BG_TASKS

    //与软件本体（比如说启动参数）相关的API
    #region HOST

    /// <summary>
    /// 软件的启动参数，正常情况下应该是一个<see cref="AppActivationArguments"/>
    /// </summary>
    object? ActivationArgs { get; }

    /// <summary>
    /// 软件当前使用的语言（切换语言后，这个值会变成目标语言，但需要重启后才生效）
    /// </summary>
    LanguageEnum Language { get; }

    #endregion

    //与界面相关的API
    #region PAGE

    /// <summary>
    /// 软件窗口，注意软件处于最小化到托盘时该值为null
    /// </summary>
    /// <returns></returns>
    Window? GetMainWindow();

    /// <summary>
    /// 跳转到指定页面
    /// </summary>
    /// <param name="page">要跳转到的界面</param>
    /// <param name="parameter">跳转参数，可为空</param>
    void NavigateTo(PageEnum page, object? parameter = null);

    /// <summary>
    /// 跳转到当前插件自己的 <see cref="Page"/>。
    /// </summary>
    /// <remarks>
    /// <paramref name="pageType"/> 需要是当前插件提供的页面类型，且必须继承自 <see cref="Page"/>。<br/>
    /// 若 <paramref name="title"/> 为空，宿主会默认使用插件名作为标题。<br/>
    /// 宿主会在需要时自动切换到 UI 线程执行导航。
    /// </remarks>
    /// <param name="pageType">要跳转到的插件页面类型</param>
    /// <param name="title">页面标题，可为空</param>
    /// <param name="parameter">跳转参数，可为空</param>
    /// <param name="clearNavigation">是否清空返回栈</param>
    void NavigateTo(Type pageType, string? title = null, object? parameter = null, bool clearNavigation = false);

    #endregion

    #region SIDEBAR

    /// <summary>
    /// 向侧边栏注册一个按钮。
    /// </summary>
    /// <remarks>
    /// 按钮的显示/隐藏由宿主统一管理；用户可以在设置中关闭插件注册的按钮。<br/>
    /// 同一个插件内，<see cref="SidebarButtonInfo.Id"/> 需要稳定且唯一，用于持久化显示状态。<br/>
    /// 宿主会在 UI 线程调用回调；若插件需要耗时操作，请自行切换线程或使用异步实现。<br/>
    /// 重复注册同一个 Id 会覆盖旧按钮。
    /// </remarks>
    void RegisterSidebarButton(SidebarButtonInfo button, Func<Task> onClick);

    /// <summary>
    /// 取消注册当前插件的某个侧边栏按钮。
    /// </summary>
    void UnregisterSidebarButton(string buttonId);

    #endregion

    #region UTILS

    /// <summary>
    /// 下载图片并保存到本地
    /// </summary>
    /// <param name="imageUrl">图片链接</param>
    /// <param name="imageName">图片名，<b>不用后缀名</b></param>
    /// <param name="client">自定义httpClient，若不指定则使用potatovn的默认HttpClient</param>
    /// <param name="onException">异常回调</param>
    /// <returns>下载后图片路径，若下载失败返回null</returns>
    public Task<string?> DownloadImageAsync(string imageUrl, string imageName, HttpClient? client,
        Action<Exception>? onException = null);

    /// <summary>
    /// 获取插件所在路径
    /// </summary>
    /// <returns></returns>
    public string GetPluginPath();

    /// <summary>
    /// 在主线程执行某个操作（一般用于UI相关操作）
    /// </summary>
    /// <param name="action"></param>
    public void InvokeOnMainThread(Action action);

    /// <summary>
    /// 在主线程执行异步操作，并等待操作完成。
    /// </summary>
    public Task InvokeOnMainThreadAsync(Func<Task> action);

    #endregion

    #region OBSOLETE_APIS

    /// <summary>
    /// 旧版本地游戏添加接口。
    /// </summary>
    /// <param name="path">游戏根目录绝对路径</param>
    /// <param name="force">未搜刮到游戏信息时是否强制添加</param>
    /// <param name="requireConfirm">是否显示搜刮确认界面</param>
    /// <returns>新增或关联安装实例后的逻辑游戏</returns>
    [Obsolete($"请使用{nameof(AddGameInstallation)}")]
    public Task<Galgame> AddGame(string path, bool force = true, bool requireConfirm = true);

    #endregion
}
