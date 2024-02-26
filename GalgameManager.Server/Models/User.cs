using System.ComponentModel.DataAnnotations;
using GalgameManager.Server.Enums;

namespace GalgameManager.Server.Models;

public class User
{
    public int Id { get; set; }
    [MaxLength(100)] public string UserName { get; set; } = string.Empty;
    [MaxLength(100)] public string DisplayUserName { get; set; } = string.Empty;
    [MaxLength(200)] public string PasswordHash { get; set; } = string.Empty;
    public UserType Type { get; set; }
    [MaxLength(30)] public string AvatarLoc { get; set; } = string.Empty;
    
    public long UsedSpace { get; set; }
    public required long TotalSpace { get; set; }
    
    public long LastGalChangedTimeStamp { get; set; }
    public List<Galgame>? Galgames { get; set; }

    public int BangumiId { get; set; }
}