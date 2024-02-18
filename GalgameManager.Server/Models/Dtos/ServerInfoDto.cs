namespace GalgameManager.Server.Models;

public class ServerInfoDto
{
    public required bool BangumiOAuth2Enable { get; set; }
    public required bool DefaultLoginEnable { get; set; }
    public required bool BangumiLoginEnable { get; set; }
}