using GalgameManager.Server.Enums;

namespace GalgameManager.Server.Models;

public class UserDto(User user)
{
    public int Id { get; set; } = user.Id;
    public string UserName { get; set; } = user.UserName;
    public string UserDisplayName { get; set; } = user.DisplayUserName;
    public UserType Type { get; set; } = user.Type;
}