﻿namespace GalgameManager.Enums;

public static class KeyValues
{
    //设置与杂项
    public const string BangumiOAuthState= "bangumiOAuthState";
    public const string BangumiOAuthStateLastUpdate = "bangumiOAuthStateLastUpdate";
    public const string RssType = "rssType";
    public const string OverrideLocalName = "overrideLocalName";
    public const string OverrideLocalNameWithChinese = "overrideLocalNameWithChinese";
    public const string SyncPlayStatusWhenPhrasing = "syncPlayStatusWhenPhrasing"; //bool, 是否在获取游戏信息时同步游玩状态
    public const string RemoteFolder = "remoteFolder";
    public const string SortKeys = "sortKeys";
    public const string SortKeysAscending = "sortKeysAscending";
    public const string SearchChildFolder = "searchChildFolder";
    public const string SearchChildFolderDepth = "searchChildFolderDepth";
    public const string IgnoreFetchResult = "ignoreFetchResult";
    public const string RegexPattern = "regexPattern";
    public const string RegexIndex = "regexIndex";
    public const string RegexRemoveBorder = "regexRemoveBorder";
    public const string GameFolderMustContain = "gameFolderMustContain";
    public const string GameFolderShouldContain = "gameFolderShouldContain";
    public const string FaqLastUpdate = "faqLastUpdate";
    public const string SaveBackupMetadata = "saveBackupMetadata";
    public const string DisplayedUpdateVersion = "displayedUpdateVersion";
    public const string CustomPasswordSaverName = "PotatoVN";
    public const string CustomPasswordDisplayName = "CustomPassword";
    public const string LastUpdateCheckDate = "lastUpdateCheckDate"; // DateTime,上次检查更新的时间
    public const string LastUpdateCheckResult = "lastUpdateCheckResult"; // bool,上次检查更新的结果
    public const string LastNoticeUpdateVersion = "lastNoticeUpdateVersion"; // string,上次通知更新的版本
    public const string AutoCategory = "autoCategory"; // bool,是否自动分类
    public const string AuthenticationType = "authenticationType"; // AuthenticationType,身份验证类型
    public const string FontInstalled = "fontInstalled"; //bool, 是否安装了Segoe Fluent Icons字体
    public const string SyncGames = "syncGames"; //bool, 是否同步游戏（游玩时长/状态/列表）
    public const string SyncTo = "syncTo"; //map<mac:string, id:int>，每台设备merge到的commit id
    
    //游玩相关
    public const string RecordOnlyWhenForeground = "recordOnlyWhenForeground"; //bool, 是否只在游戏窗口在前台时记录游玩时间
    public const string PlayingWindowMode = "playingWindowMode"; // WindowMode,游玩时窗口模式
    
    //启动与跳转相关
    public const string QuitStart = "quitStart"; //bool, 是否在jump list跳转打开游戏时启动游戏
    public const string CategoryGroup = "categoryGroup"; // string，分类页展示的分类组
    public const string StartPage = "startPage"; // PageEnum,启动时显示的页面
    
    //数据相关
    public const string GalgameFolders = "galgameFolders";
    public const string Galgames = "galgames";
    public const string Filters = "filters";
    public const string CategoryGroups = "categoryGroups"; // List<CategoryGroup>,分类组
    
    //主页显示相关
    public const string DisplayPlayTypePolygon = "displayPlayTypePolygon"; //bool, 主页是否显示游玩状态的小三角形
    public const string FixHorizontalPicture = "fixHorizontalPictrue"; //bool, 主页是否裁剪横图
    public const string DisplayVirtualGame = "displayVirtualGame"; //bool, 主页是否显示虚拟游戏
    public const string SpecialDisplayVirtualGame = "specialDisplayVirtualGame"; //bool, 主页是否特殊显示虚拟游戏（降低透明度）
    
    //软件本体设置相关
    public const string MemoryImprove = "memoryImprove"; //bool, 是否启用内存优化
    public const string UploadData = "uploadData"; // bool,是否将匿名数据上传到AppCenter
    public const string CloseMode = "closeMode"; // WindowMode,关闭模式，Normal（表示未设定）/Close/SystemTray
    
    //是否执行过某种升级, bool
    public const string IdFromMixedUpgraded = "idFromMixedUpgraded"; //其他信息源id从mixed中获取
    public const string SaveFormatUpgraded = "saveFormatUpgraded"; //设置格式升级
    public const string SortKeysUpgraded = "sortKeysUpgraded"; //排序格式升级
    public const string OAuthUpgraded = "OAuthUpgraded"; //OAuth升级
    public const string SavePathUpgraded = "savePathUpgraded"; //存档路径升级
    public const string GameSyncUpgraded = "gameSyncUpgraded"; //游戏同步升级
    
    
    //废弃Key，只读，仅用于升级
    public const string BangumiToken = "bangumiToken";
}