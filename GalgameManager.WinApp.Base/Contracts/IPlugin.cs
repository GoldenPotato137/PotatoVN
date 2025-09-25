using GalgameManager.WinApp.Base.Models;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IPlugin
{
    /// <summary>
    /// 返回本插件的信息（ID、插件名、支持版本等）
    /// </summary>
    public PluginInfo Info { get; }
}