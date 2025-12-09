using System.Collections.Generic;
using System.Linq;
using GalgameManager.Enums;

namespace GalgameManager.Models.LLM;

/// <summary>
/// Chat消息
/// </summary>
public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    public ChatMessage() { }

    public ChatMessage(ChatRole role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// Chat响应结果（支持单个和批量响应）
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// Provider名称
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Provider显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 消息角色
    /// </summary>
    public ChatRole Role { get; set; }

    /// <summary>
    /// 响应内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// 子响应集合（用于批量响应）
    /// </summary>
    public List<ChatResponse>? Items { get; set; }

    /// <summary>
    /// 是否是批量响应
    /// </summary>
    public bool IsBatch => Items?.Count > 0;

    /// <summary>
    /// 创建单个成功响应
    /// </summary>
    public static ChatResponse CreateSuccess(string providerName, string displayName, ChatRole role, string content, long responseTimeMs = 0)
    {
        return new ChatResponse
        {
            ProviderName = providerName,
            DisplayName = displayName,
            IsSuccess = true,
            Role = role,
            Content = content,
            ResponseTimeMs = responseTimeMs
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static ChatResponse CreateFailure(string providerName, string displayName, string error, long responseTimeMs = 0)
    {
        return new ChatResponse
        {
            ProviderName = providerName,
            DisplayName = displayName,
            IsSuccess = false,
            Role = ChatRole.Assistant,
            Content = string.Empty,
            Error = error,
            ResponseTimeMs = responseTimeMs
        };
    }

    /// <summary>
    /// 创建批量响应
    /// </summary>
    public static ChatResponse CreateBatch(IEnumerable<ChatResponse> items)
    {
        return new ChatResponse
        {
            ProviderName = "Batch",
            DisplayName = "批量响应",
            IsSuccess = items.All(x => x.IsSuccess),
            Role = ChatRole.Assistant,
            Content = string.Empty,
            Items = items.ToList()
        };
    }
}