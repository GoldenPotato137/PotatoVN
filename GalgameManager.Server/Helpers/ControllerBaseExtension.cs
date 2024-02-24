using System.Security.Claims;
using GalgameManager.Server.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Helpers;

public static class ControllerBaseExtension
{
    public static int GetUserId(this ControllerBase controller)
    {
        return int.Parse(controller.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "-1");
    }
    
    public static UserType GetUserType(this ControllerBase controller)
    {
        try
        {
            var type = controller.User.FindFirst(ClaimTypes.Role)?.Value ?? UserType.User.ToString();
            return Enum.Parse<UserType>(type);
        }
        catch
        {
            return UserType.User;
        }
    }
}