using System.Collections.Generic;

namespace GalgameManager.Models.LLM;

/// <summary>
/// 批量Chat响应结果
/// </summary>
public class BatchChatResponse
{
    /// <summary>
    /// 成功的响应
    /// </summary>
    public List<ChatResult> Successes { get; set; } = new();

    /// <summary>
    /// 失败的响应
    /// </summary>
    public List<ChatError> Errors { get; set; } = new();

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool AllSuccessful => Errors.Count == 0 && Successes.Count > 0;
}

/// <summary>
/// 单个Chat成功响应
/// </summary>
public class ChatResult
{
    /// <summary>
    /// Provider名称
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Provider显示名称
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public ChatMessage Message { get; set; }

    /// <summary>
    /// 响应时间（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; set; }
}

/// <summary>
/// 单个Chat错误响应
/// </summary>
public class ChatError
{
    /// <summary>
    /// Provider名称
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Provider显示名称
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// 异常详情
    /// </summary>
    public System.Exception? Exception { get; set; }
}