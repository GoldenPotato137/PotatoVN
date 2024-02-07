using System.ComponentModel.DataAnnotations;

namespace GalgameManager.Server.Models;

public class UserRegisterDto
{
    [Required] public required string UserName { get; set; }
    [Required] public required string Password { get; set; }
}

public class UserLoginDto
{
    [Required] public required string UserName { get; set; }
    [Required] public required string Password { get; set; }
}

public class UserLoginViaBgmDto
{
    /// <summary>
    /// bangumi token
    /// </summary>
    [Required] public required string BgmToken { get; set; }
}