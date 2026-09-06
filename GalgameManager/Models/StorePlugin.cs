using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Enums;

namespace GalgameManager.Models;

/// <summary>
/// 用来展示插件商店里的插件
/// </summary>
public partial class StorePlugin : ObservableObject
{
    /// 插件所在仓库名
    public required string RepoName;
    /// 插件ID
    [ObservableProperty] private Guid _id;
    /// 插件名
    [ObservableProperty] private string _name = string.Empty;
    /// 插件简述
    [ObservableProperty] private string _descriptionShort = string.Empty;
    /// 插件详细描述
    [ObservableProperty] private string _descriptionDetailed = string.Empty;
    [ObservableProperty] private string? _developer;
    [ObservableProperty] private string? _developerUrl;
    [ObservableProperty] private string? _projectUrl;
    /// 插件图标URL
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Logo))] private string? _logoUrl;
    /// 插件图标本地路径
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Logo))] private string? _logoPath;
    public string? Logo => !string.IsNullOrEmpty(LogoPath) ? LogoPath : LogoUrl;
    /// 插件发布日期
    [ObservableProperty] private DateTime _releaseDate;
    /// 插件各个版本与下载链接
    public List<StorePluginVersion> Versions { get; set; } = [];
    /// 插件类别
    public List<PluginType> Types { get; set; } = [];

    [ObservableProperty] private StorePluginStatus _status = StorePluginStatus.NotInstalled;
    [ObservableProperty] private Version? _installedVersion;

    /// <summary>
    /// 根据已安装的版本刷新<see cref="InstalledVersion"/>与<see cref="Status"/>。<br/>
    /// 注意：会改动ObservableProperty，若插件已经在界面上显示则需要在UI线程调用。
    /// </summary>
    /// <param name="installedVersion">已安装的版本，为null表示未安装</param>
    public void UpdateStatus(Version? installedVersion)
    {
        InstalledVersion = installedVersion;
        if (installedVersion is null)
        {
            Status = StorePluginStatus.NotInstalled;
            return;
        }
        // 装的不是最新版（用户可以在详情对话框里选旧版本安装）时仍然算作有更新
        Status = Versions.Count > 0 && Versions[0].Version > installedVersion
            ? StorePluginStatus.UpdateAvailable
            : StorePluginStatus.Installed;
    }
}

public enum StorePluginStatus
{
    NotInstalled,
    Installed,
    UpdateAvailable
}

public class StorePluginVersion
{
    public Version Version = new();
    public string DownloadUrl = string.Empty;
    public DateTime ReleaseDate;
}

public class ToInstallStorePlugin
{
    public StorePlugin Plugin = null!;
    public StorePluginVersion Version = null!;
}
