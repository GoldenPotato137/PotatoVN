using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.Models.LLM;

namespace GalgameManager.Contracts.Services;

/// <summary>
/// LLM服务接口
/// </summary>
internal interface ILlMService
{
    /// <summary>
    /// 流式对话
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应</returns>
    IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 普通对话（非流式）
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整响应内容</returns>
    Task<string> ChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试连接
    /// </summary>
    /// <returns>是否连接成功</returns>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// 获取可用模型列表
    /// </summary>
    /// <returns>模型名称列表</returns>
    Task<string[]> GetModelsAsync();
}