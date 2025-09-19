using System;

namespace GalgameManager.WinApp.Base.Models;

public class PluginInfo
{
    /// <summary>
    /// 插件ID，请务必保证你这个插件的ID是<b>不变</b>的 <br/>
    /// 可以考虑使用在线UUID生成器生成一个并硬编码在代码里
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// 插件名
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// 插件描述
    /// </summary>
    public required string Description { get; set; }
}