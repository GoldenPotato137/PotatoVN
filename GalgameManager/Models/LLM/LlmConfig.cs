using System;
using System.Collections.Generic;

namespace GalgameManager.Models.LLM;

/// <summary>
/// LLM Provider类型
/// </summary>
public enum LlMProviderType
{
    OpenAI,
    Local
}

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
    /// Provider类型
    /// </summary>
    public LlMProviderType Type { get; set; } = LlMProviderType.OpenAI;

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
    /// 系统提示词（用于设置AI的角色和行为）
    /// </summary>
    public string SystemPrompt { get; set; } = "";

    /// <summary>
    /// 用户预设提示词前缀（会自动添加到每个用户请求前）
    /// </summary>
    public string UserPromptPrefix { get; set; } = "";

    /// <summary>
    /// 用户预设提示词后缀（会自动添加到每个用户请求后）
    /// </summary>
    public string UserPromptSuffix { get; set; } = "";

    /// <summary>
    /// 额外参数（如temperature、max_tokens等）
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 处理用户消息，添加预设的前缀和后缀
    /// </summary>
    /// <param name="userMessage">原始用户消息</param>
    /// <returns>处理后的消息</returns>
    public string ProcessUserMessage(string userMessage)
    {
        var result = userMessage;

        if (!string.IsNullOrEmpty(UserPromptPrefix))
        {
            result = $"{UserPromptPrefix}\n\n{result}";
        }

        if (!string.IsNullOrEmpty(UserPromptSuffix))
        {
            result = $"{result}\n\n{UserPromptSuffix}";
        }

        return result;
    }
}