namespace GalgameManager.Enums;

public static class KeyValues
{
    public const string BangumiAccessToken = "bangumiAccessToken";
    public const string BangumiRefreshToken = "bangumiRefreshToken";
    public const string RssType = "rssType";
    public const string GalgameFolders = "galgameFolders";
    public const string Galgames = "galgames";
    public const string OverrideLocalName = "overrideLocalName";
    public const string OverrideLocalNameWithCNByBangumi = "overrideLocalNameWithCNByBangumi";
    public const string RemoteFolder = "remoteFolder";
    public const string QuitStart = "quitStart";
    public const string SortKey1 = "sortKey1";
    public const string SortKey2 = "sortKey2";
    public const string SearchChildFolder = "searchChildFolder";
    public const string SearchChildFolderDepth = "searchChildFolderDepth";
    public const string IgnoreFetchResult = "ignoreFetchResult";
    public const string RegexPattern = "regexPattern";
    public const string RegexIndex = "regexIndex";
    public const string RegexRemoveBorder = "regexRemoveBorder";
    public const string GameFolderMustContain = "gameFolderMustContain";
    public const string GameFolderShouldContain = "gameFolderShouldContain";
    public const string FaqLastUpdate = "faqLastUpdate";
    public const string FixHorizontalPicture = "fixHorizontalPictrue";
    public const string SaveBackupMetadata = "saveBackupMetadata";
    public const string Filters = "filters";
    public const string DisplayedUpdateVersion = "displayedUpdateVersion";
    public const string CustomPasswordSaverName = "PotatoVN";
    public const string CustomPasswordDisplayName = "CustomPassword";
    public const string LastUpdateCheckDate = "lastUpdateCheckDate"; // DateTime,上次检查更新的时间
    public const string LastUpdateCheckResult = "lastUpdateCheckResult"; // bool,上次检查更新的结果
    public const string LastNoticeUpdateVersion = "lastNoticeUpdateVersion"; // string,上次通知更新的版本
    public const string UploadData = "uploadData"; // bool,是否将匿名数据上传到AppCenter
    public const string CategoryGroups = "categoryGroups"; // List<CategoryGroup>,分类组
    public const string CategoryGroup = "categoryGroup"; // string，分类页展示的分类组
    public const string AutoCategory = "autoCategory"; // bool,是否自动分类
    public const string StartPage = "startPage"; // PageEnum,启动时显示的页面
    public const string AuthenticationType = "authenticationType"; // AuthenticationType,身份验证类型
    
    
    //是否执行过某种升级, bool
    public const string IdFromMixedUpgraded = "idFromMixedUpgraded"; //其他信息源id从mixed中获取
}