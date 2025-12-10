using GalgameManager.Enums;
using System.Collections.Generic;

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
/// 流式聊天块
/// </summary>
public sealed class StreamingChatChunk
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
    public ChatRole Role { get; init; }

    /// <summary>
    /// 内容块 - 使用ReadOnlyMemory避免字符串拷贝
    /// </summary>
    public ReadOnlyMemory<char> Content { get; init; }

    /// <summary>
    /// 内容的字符串表示
    /// </summary>
    public string ContentString => Content.ToString();

    /// <summary>
    /// 是否为结束标记
    /// </summary>
    public bool IsEnd { get; init; }

    /// <summary>
    /// 错误信息（如果有）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 是否有错误
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);
}

/// <summary>
/// 批量流式聊天响应处理器
/// </summary>
public class BatchStreamingHandler : IDisposable
{
    private readonly Dictionary<string, StreamingChatState> _states = new();
    private bool _disposed = false;

    /// <summary>
    /// 总数
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// 完成数量
    /// </summary>
    public int CompletedCount { get; private set; }

    /// <summary>
    /// 错误数量
    /// </summary>
    public int ErrorCount => _states.Values.Count(s => s.HasError);

    /// <summary>
    /// 是否全部完成
    /// </summary>
    public bool AllCompleted => CompletedCount + ErrorCount >= TotalCount;

    /// <summary>
    /// 初始化批量响应
    /// </summary>
    public void Initialize(int totalCount)
    {
        TotalCount = totalCount;
        _states.Clear();
        CompletedCount = 0;
    }

    /// <summary>
    /// 更新流式响应状态
    /// </summary>
    public void UpdateChunk(string providerName, StreamingChatChunk chunk)
    {
        if (!_states.TryGetValue(providerName, out var state))
        {
            state = new StreamingChatState();
            _states[providerName] = state;
        }

        // 累积内容
        if (!chunk.Content.IsEmpty)
        {
            state.AccumulatedContent += chunk.ContentString;
        }

        // 检查是否结束
        if (chunk.IsEnd || chunk.HasError)
        {
            state.IsCompleted = true;
            state.HasError = chunk.HasError;
            state.Error = chunk.Error;
            if (!chunk.HasError)
            {
                CompletedCount++;
            }
        }
    }

    /// <summary>
    /// 获取指定Provider的累积内容
    /// </summary>
    public string GetAccumulatedContent(string providerName)
    {
        return _states.TryGetValue(providerName, out var state) ? state.AccumulatedContent : "";
    }

    /// <summary>
    /// 获取所有Provider的完成状态
    /// </summary>
    public IReadOnlyDictionary<string, StreamingChatState> GetStates()
    {
        return _states;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _states.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// 流式聊天状态
/// </summary>
public class StreamingChatState
{
    public string AccumulatedContent { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool HasError { get; set; }
    public string Error { get; set; } = "";
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