using System.ComponentModel.DataAnnotations;

namespace GalgameManager.Server.Models;

public class OssRecord
{
    [Key][MaxLength(250)] public required string Key { get; set; }
    public User? User { get; set; }
    public required int UserId { get; set; }
    public required long Size { get; set; }
}