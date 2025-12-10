using GalgameManager.Enums;
using System.Collections.Generic;

namespace GalgameManager.Models.LLM;

/// <summary>
/// Chat消息
/// </summary>
public record struct ChatMessage(ChatRole Role, string Content);

/// <summary>
/// LLM 请求对象
/// </summary>
public record LlmRequest(string Prompt, string[]? Models = null);

/// <summary>
/// 流式聊天块 - 优化为结构体以减少GC压力
/// </summary>
public readonly record struct StreamingChatChunk
{
    /// <summary>
    /// Provider名称
    /// </summary>
    public string ProviderName { get; init; } = "";

    /// <summary>
    /// Provider显示名称
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// 消息角色
    /// </summary>
    public ChatRole Role { get; init; } = ChatRole.Assistant;

    /// <summary>
    /// 内容块
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// 是否为结束标记
    /// </summary>
    public bool IsEnd { get; init; } = false;

    /// <summary>
    /// 错误信息（如果有）
    /// </summary>
    public string? Error { get; init; } = null;

    /// <summary>
    /// 是否有错误
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    public StreamingChatChunk() { }
}

/// <summary>
/// 聊天元数据
/// </summary>
public class ChatMetadata
{
    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Token数量（如果支持）
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// 额外参数
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
