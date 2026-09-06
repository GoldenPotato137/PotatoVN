namespace GalgameManager.Server.Models;

public class ServerInfoDto
{
    public required bool BangumiOAuth2Enable { get; set; }
    public required bool HikarinagiOAuth2Enable { get; set; }
    public required bool DefaultLoginEnable { get; set; }
    public required bool BangumiLoginEnable { get; set; }
    /// <summary>
    /// 是否支持同步GalgameStaff
    /// </summary>
    public required bool GalgameStaffAvailable { get; set; }
    /// <summary>
    /// 是否支持同步Staff
    /// </summary>
    public required bool StaffEnable { get; set; }
    /// <summary>
    /// 服务器版本号
    /// </summary>
    public string ServerVersion { get; set; } = string.Empty;
}
