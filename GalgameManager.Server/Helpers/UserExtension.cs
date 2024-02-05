using GalgameManager.Server.Models;

namespace GalgameManager.Server.Helpers;

public static class UserExtension
{
    public static IEnumerable<UserDto> ToDto(this IEnumerable<User> users)
    {
        return users.Select(x => new UserDto(x));
    }
}