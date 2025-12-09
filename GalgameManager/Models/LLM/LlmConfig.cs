using System;
using System.Collections.Generic;

namespace GalgameManager.Models.LLM;

/// <summary>
/// LLM配置
/// </summary>
public class LlMConfig
{
    /// <summary>
    /// 默认使用的Provider名称（如果为空，使用第一个Provider）
    /// </summary>
    public string? DefaultProvider { get; set; }

    /// <summary>
    /// 所有Provider配置
    /// </summary>
    public List<LlMProvider> Providers { get; set; } = new();
}

/// <summary>
/// LLM Provider配置
/// </summary>
public class LlMProvider
{
    /// <summary>
    /// Provider名称（唯一标识）
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Provider类型（类名，如 "OpenAiService", "LocalLlmService"）
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// API基础URL
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// API密钥
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// 是否启用（可用于批量Chat时排除某些Provider）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 额外参数（如temperature、max_tokens等）
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}