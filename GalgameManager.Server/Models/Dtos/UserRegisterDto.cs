using System.ComponentModel.DataAnnotations;

namespace GalgameManager.Server.Models;

public class UserRegisterDto
{
    [Required] public required string UserName { get; set; }
    [Required] public required string Password { get; set; }
}