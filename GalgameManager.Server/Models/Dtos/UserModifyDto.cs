namespace GalgameManager.Server.Models;

public class UserModifyDto
{
    public string? UserDisplayName { get; set; }
    public string? AvatarLoc { get; set; }
    public string? OldPassword { get; set; }
    public string? NewPassword { get; set; }
}