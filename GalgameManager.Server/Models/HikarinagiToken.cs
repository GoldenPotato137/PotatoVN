namespace GalgameManager.Server.Models;

public class HikarinagiToken
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required string TokenType { get; set; }
    public required string Scope { get; set; }
    public long Expires { get; set; }
}
