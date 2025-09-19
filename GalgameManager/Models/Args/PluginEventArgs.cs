using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.Models;

/// <summary>
/// 当插件被加载时触发
/// </summary>
public class PluginLoadArgs
{
    public required IPlugin Plugin { get; init; }
}