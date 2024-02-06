namespace GalgameManager.Models;

public class PvnAccount
{
    public required int Id;
    public required string UserName;
    public required string UserDisplayName;
    public string? Avatar;
    public required long ExpireTimestamp;
}