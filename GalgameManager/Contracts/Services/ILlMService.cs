using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.Models.LLM;

namespace GalgameManager.Contracts.Services;

/// <summary>
/// LLM服务接口 - 只支持流式响应
/// </summary>
public interface ILlMService
{
    /// <summary>
    /// 流式对话实现
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="config">Provider配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应</returns>
    IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(List<ChatMessage> messages, LlMProvider config, CancellationToken cancellationToken = default);
}