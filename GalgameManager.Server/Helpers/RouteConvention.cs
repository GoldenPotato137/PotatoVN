using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace GalgameManager.Server.Helpers;

public class RouteConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        controller.ControllerName = ToSnakeCase(controller.ControllerName);
    }

    private static string ToSnakeCase(string str)
    {
        return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())).ToLower();
    }
}