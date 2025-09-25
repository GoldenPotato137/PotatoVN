namespace GalgameManager.Enums;

public static class KeyValues
{
    //设置与杂项
    public const string RemoteFolder = "remoteFolder";
    public const string SortKeys = "sortKeys";
    public const string SortKeysAscending = "sortKeysAscending";
    public const string SearchChildFolder = "searchChildFolder";
    public const string IgnoreFetchResult = "ignoreFetchResult";
    public const string RegexPattern = "regexPattern";
    public const string RegexIndex = "regexIndex";
    public const string RegexRemoveBorder = "regexRemoveBorder";
    public const string GameFolderMustContain = "gameFolderMustContain";
    public const string GameFolderShouldContain = "gameFolderShouldContain";
    public const string FaqLastUpdate = "faqLastUpdate";
    public const string DisplayedUpdateVersion = "displayedUpdateVersion";
    public const string CustomPasswordSaverName = "PotatoVN";
    public const string CustomPasswordDisplayName = "CustomPassword";
    public const string LastUpdateCheckResult = "lastUpdateCheckResult"; // bool,上次检查更新的结果
    public const string LastNoticeUpdateVersion = "lastNoticeUpdateVersion"; // string,上次通知更新的版本
    public const string IgnoredUpdateVersions = "IgnoredUpdateVersions";
    public const string AutoCategory = "autoCategory"; // bool,是否自动分类
    public const string AuthenticationType = "authenticationType"; // AuthenticationType,身份验证类型
    public const string FontInstalled = "fontInstalled"; //bool, 是否安装了Segoe Fluent Icons字体
    public const string CustomTextFileExtensions = "CustomTextFileExtensions"; // List<string>, 用户自定义的文本文件扩展名列表
    
    //账户相关
    public const string BangumiAccount= "bangumiAccount"; //BgmAccount?, Bangumi账户, 若为null则未登录
    public const string BangumiOAuthStateLastUpdate = "bangumiOAuthStateLastUpdate";
    public const string PvnServerType = "pvnServerType"; //enum: PvnServerType, 服务器类型（官方/自定义）
    public const string PvnServerEndpoint = "pvnServerEndpoint"; //string, 自定义服务器Url
    public const string PvnAccount = "pvnAccount"; //PvnAccount?, PotatoVN账户, 若为null则未登录
    public const string PvnAccountUserName = "pvnAccountUserName"; //string, PotatoVN账户名
    public const string VndbAccount = "vndbAccount"; //VndbAccount?, Vndb账户, 若为null则未登录
    
    //游玩相关
    public const string RecordOnlyWhenForeground = "recordOnlyWhenForeground"; //bool, 是否只在游戏窗口在前台时记录游玩时间
    public const string PlayingWindowMode = "playingWindowMode"; // WindowMode,游玩时窗口模式
    public const string LocaleEmulatorPath = "localeEmulatorPath"; //string?, 本地模拟器路径
    public const string MagpieTotalSwitch = "magpieTotalSwitch"; //bool, 是否启用Magpie总开关
    public const string MagpiePath = "magpiePath"; //string?, Magpie可执行文件路径
    public const string MagpieHotkeys = "magpieHotkeys"; //List<int>, Magpie快捷键 VirtualKey codes
    public const string AlwaysEnableMagpie = "AlwaysEnableMagpie"; //bool, 是否无视各个游戏设置总是启用Magpie
    public const string MinPlayTimeRecordThreshold = "minPlayTimeRecordThreshold"; //int, 记录一次游玩的最小游玩时长 (分钟)
    public const string GameMuteEnabled = "gameMuteEnabled"; //bool, 是否启用游戏静音功能
    public const string AlwaysMuteInBackground = "AlwaysMuteInBackground"; //bool, 是否无视各个游戏设置总是在后台时静音游戏
    public const string QuickMinimizeHotkeys = "quickMinimizeHotkeys"; //List<int>, 一键最小化快捷键 VirtualKey codes
    public const string AlwaysQuickMinimize = "AlwaysQuickMinimize"; //bool, 是否无视各个游戏设置总是启用一键最小化
    
    //启动与跳转相关
    public const string QuitStart = "quitStart"; //bool, 是否在jump list跳转打开游戏时启动游戏
    public const string CategoryGroup = "categoryGroup"; // string，分类页展示的分类组
    public const string StartPage = "startPage"; // PageEnum,启动时显示的页面
    public const string AutoStartWhenLogin = "autoStartWhenLogin"; //bool, 是否开机自启
    public const string MinToTrayWhenAutoStart = "minToTrayWhenAutoStart"; //bool, 开机自启时是否最小化到托盘
    
    //数据相关
    public const string GalgameSources = "galgameSources";
    ///int, 当前已经用到的最大的galgameSource的id
    public const string GalgameSourcesId = "galgameSourcesId"; 
    public const string Galgames = "galgames";
    public const string Filters = "filters";
    public const string KeepFilters = "keepFilters"; //bool, 离开界面/关闭软件时是否保留筛选器
    public const string CategoryGroups = "categoryGroups"; // List<CategoryGroup>,分类组
    public const string MultiStreamPageList = "multiStreamPageList"; //List<IGalgameManager.MultiStreamPage.Lists.IList>, 主页列表
    public const string PluginPaths = "pluginPaths"; // List<string>,插件路径列表
    //数据同步
    public const string PvnSyncTimestamp = "pvnSyncTimestamp"; //long, 上一次同步时间戳
    public const string PvnSyncStaffTimestamp = "pvnSyncStaffTimestamp"; //long, 上次同步staff的时间戳
    public const string SyncGames = "syncGames"; //bool, 是否同步游戏（游玩时长/状态/列表）
    public const string ToDeleteGames = "toDeleteGames"; //List<int>, 待删除的游戏id
    public const string SyncGameCharacters = "syncGameCharacters"; //bool, 是否同步游戏角色
    public const string SyncStaff = "syncStaff"; //bool, 是否同步staff
    public const string SyncHeaderImage = "syncHeaderImage"; //bool, 是否同步header图
    public const string ToDeleteStaff = "toDeleteStaff"; //List<int>, 待删除的staff pvn id
    
    //搜刮设置
    public const string RssType = "rssType";
    public const string OverrideLocalName = "overrideLocalName";
    public const string OverrideLocalNameWithChinese = "overrideLocalNameWithChinese";
    public const string SyncPlayStatusWhenPhrasing = "syncPlayStatusWhenPhrasing"; //bool, 是否在获取游戏信息时同步游玩状态
    public const string DownloadCharacters = "fetchCharacters"; //bool, 搜刮时是否获取角色信息
    public const string MixedPhraserOrder = "mixedPhraserOrder"; //MixedPhraserOrder,混合搜刮器的顺序
    public const string MixedPhraserEnabled = "mixedPhraserEnabled"; //MixedPhraserEnabled,混合搜刮器各个搜刮器的启用状态
    
    //显示相关
    public const string DisplayPlayTypePolygon = "displayPlayTypePolygon"; //bool, 游戏页是否显示游玩状态的小三角形
    public const string FixHorizontalPicture = "fixHorizontalPictrue"; //bool, 游戏页是否裁剪横图
    public const string DisplayVirtualGame = "displayVirtualGame"; //bool, 游戏页是否显示虚拟游戏
    public const string SpecialDisplayVirtualGame = "specialDisplayVirtualGame"; //bool, 游戏页是否特殊显示虚拟游戏（降低透明度）
    public const string MultiStreamPageAllowScroll = "multiStreamPageAllowScroll"; //bool, 主页列表是否允许横向滚动
    public const string TimeAsHour = "timeAsHour"; //bool，时间是否显示为"__h__m"，若为false则显示为"__分钟"
    public const string TransparentNavigationView = "transparentNavigationView"; //bool, 是否使用透明导航视图背景
    public const string GalgamePagePrimaryTitleType = "GalgamePagePrimaryTitleType"; //string, 游戏页主标题类型
    public const string GalgamePageSecondaryTitleType = "GalgamePageSecondaryTitleType"; //string, 游戏页副标题类型
    public const string GalgamePageNewLayout = "galgamePageNewLayout"; //bool, 游戏页是否使用新界面
    public const string GalgamePageNewLayout_ShowPainter = "galgamePageNewLayout_showPainter"; //bool, 游戏页是否显示原画
    public const string GalgamePageNewLayout_ShowSeiyu = "galgamePageNewLayout_showSeiyu"; //bool, 游戏页是否显示声优
    public const string GalgamePageNewLayout_ShowWriter = "galgamePageNewLayout_showWriter"; //bool, 游戏页是否显示剧本
    public const string GalgamePageNewLayout_ShowMusician = "galgamePageNewLayout_showMusician"; //bool, 游戏页是否显示音乐
    public const string GalgamePageNewLayout_ShowHeaderImage = "galgamePageNewLayout_showHeaderImage"; //bool, 游戏页是否显示背景图
    public const string GalgamePageNewLayout_CoverImage = "galgamePageNewLayout_coverImage"; //bool, 游戏页是否显示封面
    public const string GalgamePageNewLayout_ShowCoverWhenNoBackground = "galgamePageNewLayout_showCoverWhenNoBackground"; //bool, 游戏页是否在没有背景图时显示封面
    public const string GalgamePageNewLayout_ShowExpectedPlayTime = "galgamePageNewLayout_showExpectedPlayTime"; //bool, 游戏页是否显示预计游玩时间
    public const string GalgamePageNewLayout_ShowRating = "galgamePageNewLayout_showRating"; //bool, 游戏页是否显示评分
    public const string GalgamePageNewLayout_ShowTags = "galgamePageNewLayout_showTags"; //bool, 游戏页是否显示标签
    public const string GalgamePageNewLayout_ShowCharacters = "galgamePageNewLayout_showCharacters"; //bool, 游戏页是否显示角色
    public const string GalgameSourcePageShowSubSourceGames = "galgameSourcePageShowSubSourceGames"; //bool, 游戏源页面是否显示子源游戏
    public const string ShowGameNameInControl = "showGameNameInControl"; //bool, 游戏控件是否显示游戏名称
    public const string PrimarySortKey = "primarySortKey"; //string, 主排序的key，默认是"playtime"，可选值有：playtime（游玩时间）/lastUpdate（最后更新时间）/name（名称）/releaseDate（发售日期）/rating（评分）/category（分类）
    public const string SecondarySortKey = "SecondarySortKey"; //string, 次排序的key，默认是"playtime"，可选值有：playtime（游玩时间）/lastUpdate（最后更新时间）/name（名称）/releaseDate（发售日期）/rating（评分）/category（分类）
    public const string PrimarySortAscending = "PrimarySortAscending"; //bool, 主排序是否降序排列，默认是false（升序）
    public const string SecondarySortAscending = "SecondarySortAscending"; //bool, 次排序是否降序排列，默认是false（升序）
    public const string PrimarySortDescending = "PrimarySortDescending"; //bool, 主排序是否降序排列，默认是false（升序）
    public const string SecondarySortDescending = "SecondarySortDescending"; //bool, 次排序是否降序排列，默认是false（升序）
    public const string DefaultGameName = "defaultGameName"; //string, 默认游戏名称

    //库页面
    public const string LibraryNavBar = "libraryNavBar"; //bool, 是否显示库页面的导航栏
    public const string LibraryStatistics = "libraryStatistics"; //bool, 是否显示库页面的统计信息（当前页游戏库/游戏数）
    public const string LibrarySortKey = "LibrarySortKey"; //string, 排序的key，默认是"playtime"，可选值有：playtime（游玩时间）/lastUpdate（最后更新时间）/name（名称）/releaseDate（发售日期）/rating（评分）/category（分类）
    public const string LibraryGameSortDescending = "LibraryGameSortDescending"; //bool, 是否降序排列，默认是false（升序）
    public const string LibraryFolderSortKey = "LibraryFolderSortKey"; //string, 库文件夹排序的key
    public const string LibraryFolderSortDescending = "LibraryFolderSortDescending"; //bool, 库文件夹是否降序排列
    
    //游戏详情界面
    public const string NotifiedSteamNeedManual = "notifyedSteamNeedManual"; //bool, 是否已经通知过Steam游戏需要手动选择游戏进程
    
    //消息通知相关 (最小化到托盘时是否通知/全局消息通知)
    public const string NotifyWhenGetGalgameInFolder = "notifyWhenGetGalgameInFolder"; //bool, 完成获取文件夹内游戏
    public const string EventGetGalgameInFolderEmpty = "eventGetGalgameInFolderEmpty"; //bool, 是否通知扫描游戏空事件（即没有新游戏）
    public const string NotifyWhenUnpackGame = "notifyWhenUnpackGame"; //bool, 完成解压游戏
    public const string EventPvnSyncNotify = "eventPvnSyncNotify"; //bool, 是否通知PotatoVN同步事件
    public const string EventPvnSyncEmptyNotify = "eventPvnSyncEmptyNotify"; //bool, 是否通知PotatoVN同步空事件（即已是最新）
    
    //软件本体设置相关
    public const string MemoryImprove = "memoryImprove"; //bool, 是否启用内存优化
    public const string UploadData = "uploadData"; // bool,是否将匿名数据上传到AppCenter
    public const string CloseMode = "closeMode"; // WindowMode,关闭模式，Normal（表示未设定）/Close/SystemTray
    public const string DevelopmentMode = "developmentMode"; //bool, 是否开发模式
    public const string LastError = "lastError"; //string, 上次错误信息
    public const string Language = "language"; //string, 语言设置，en-US/zh-CN/zh-TW
    public const string BackgroundMaterial = "backgroundMaterial"; //string, 背景素材，Mica, Mica Alt,Desktop Acrylic
    public const string UpdateType = "UpdateType";
    public const string UpdateUrl = "UpdateUrl";
    public const string IsBetaChannel = "isBetaChannel";


    //是否执行过某种升级, bool
    public const string DataStatus = "dataStatus"; //LocalSettingStatus, 用于描述某PotatoVN数据的状态
    public const string IdFromMixedUpgraded = "idFromMixedUpgraded"; //其他信息源id从mixed中获取
    public const string SaveFormatUpgraded = "saveFormatUpgraded"; //设置格式升级
    public const string SortKeysUpgraded = "sortKeysUpgraded"; //排序格式升级
    public const string OAuthUpgraded = "OAuthUpgraded"; //BgmOAuth升级1
    public const string OAuthUpgraded2 = "OAuthUpgraded2"; //BgmOAuth升级2
    public const string SavePathUpgraded = "savePathUpgraded"; //存档路径升级
    public const string GameSyncUpgraded = "gameSyncUpgraded"; //游戏同步升级
    public const string MixedPhraserOrderVersion = "mixedPhraserOrderVersion"; //int，当前配置中的混合搜刮器顺序的版本
    
    
    //废弃Key，只读，仅用于升级
    public const string BangumiToken = "bangumiToken";
    public const string BangumiOAuthState= "bangumiOAuthState"; //BgmAccount?, Bangumi账户, 若为null则未登录
    public const string GalgameFolders = "galgameFolders"; //旧游戏文件夹，仅用于升级
    public const string SyncTo = "syncTo"; //map<mac:string, id:int>，每台设备merge到的commit id
    public const string SearchChildFolderDepth = "searchChildFolderDepth"; //int, 搜索子文件夹的深度，默认是0（不搜索），新版本改成智能搜索，不需要这个了
    public const string UpdateBatchPath = "UpdateBatchPath";
    public const string SaveBackupMetadata = "saveBackupMetadata"; //bool, 是否备份元数据到存档文件夹（原来是全局设置，新版本改成支持各个库独立设置了）
}
