using GalgameManager.Services;

namespace GalgameManager.Models;

/// <summary>
/// 用于描述某PotatoVN数据的升级情况 <br/>
/// 方便跨版本数据导出与导入
/// </summary>
public class LocalSettingStatus
{
    /// LocalSettingService, v1.8.0, 将原先的一整个巨大的LocalSettings.json按key拆分成多个文件
    public bool LargerFileSeparateUpgraded = false;

    /// GalgameSourceCollectionService, v1.8.0, 修改存储库的结构
    /// <seealso cref="GalgameSourceCollectionService.SourceUpgradeAsync"/>
    public bool GalgameSourceFormatUpgrade = false;

    /// GalgameSourceCollectionService, v1.8.6, 添加虚拟游戏库
    public bool GalgameSourceAddVirtualSource = false;

    /// GalgameSourceCollectionService, 将全局SaveBackupMetadata设置迁移到各个库的SaveMetaBackup属性
    public bool MetaBackupPerSourceUpgrade = false;
    /// GalgameSourceCollectionService, 检测现有的每个库是否为可移动库
    public bool GalgameSourceRemoveableUpgrade = false;
    // GalgameSourceCollectionService，将旧版游戏级启动设置迁移到安装实例
    public bool GalgameMultiInstallUpgrade = false;

    /// CategoryService, v1.8.0, 改变分类中游戏索引格式
    public bool CategoryGameIndexUpgrade = false;

    /// CategoryService, v1.8.0, 给各分类添加LastPlayed字段
    public bool CategoryAddLastPlayed = false;

    /// CategoryService, v1.8.0, 添加"想玩"分类
    public bool CategoryAddWantToPlay = false;

    /// GalgameSourceCollectionService，GalgameDetectedSavePosition => GalgameDetectedSavePath的迁移， v1.10.0
    public bool GalgameDetectedSavePath = false;


    // 数据存储数据库化，对于导出的数据永远为false（导出数据采用json格式），v1.9
    /// gameService是否已升级为LiteDB
    public bool GameLiteDbUpgrade;
    public bool CategoryLiteDbUpgrade;
    public bool SourceLiteDbUpgrade;
    public bool MultiStreamPageLiteDbUpgrade;


    /// galgameCollectionService是否已处理过导入
    public bool ImportGalgame = true;
    /// galgameSourceCollectionService是否已处理过导入
    public bool ImportGalgameSource = true;
    /// categoryService是否已处理过导入
    public bool ImportCategory = true;
    /// staffService是否已处理过导入
    public bool ImportStaff = true;
    /// 游戏列表页/库页设置（排序等）与背景图设置是否已处理过导入
    public bool ImportPageSettings = true;
    public void SetToExport()
    {
        ImportGalgame = false;
        ImportGalgameSource = false;
        ImportCategory = false;
        ImportStaff = false;
        ImportPageSettings = false;
        GameLiteDbUpgrade = false;
        CategoryLiteDbUpgrade = false;
        SourceLiteDbUpgrade = false;
        MultiStreamPageLiteDbUpgrade = false;
        GalgameMultiInstallUpgrade = false;
    }

    public LocalSettingStatus Clone() => (LocalSettingStatus)MemberwiseClone();
}
