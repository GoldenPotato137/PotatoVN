namespace GalgameManager.Server.Models;

public class BangumiToken
{
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public long Expires { get; set; }
    public int UserId { get; set; }
}