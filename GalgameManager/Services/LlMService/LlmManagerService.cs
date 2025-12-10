using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Models.LLM;

namespace GalgameManager.Services;

/// <summary>
/// LLM管理器 - 只支持流式响应
/// </summary>
public class LlmManagerService : ILlMService
{
    private readonly ILocalSettingsService _localSettingsService;
    private LlMConfig? _cachedConfig;

    // 缓存的Service实例，避免重复创建
    private readonly Dictionary<LlMProviderType, ILlMService> _serviceCache = new();

    public LlmManagerService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    private async Task<LlMConfig> GetConfigAsync()
    {
        _cachedConfig ??= await _localSettingsService.ReadSettingAsync<LlMConfig>(KeyValues.LlmConfig) ?? new LlMConfig();
        return _cachedConfig;
    }

    private async Task<LlMProvider> GetActiveProviderAsync()
    {
        var config = await GetConfigAsync();
        var provider = config.Providers.FirstOrDefault(p => p.Name == config.DefaultProvider) ?? config.Providers.FirstOrDefault();

        if (provider == null)
        {
            provider = new LlMProvider
            {
                Name = "Default OpenAI",
                Type = LlMProviderType.OpenAiService,
                BaseUrl = "https://api.openai.com/v1/",
                Model = "gpt-3.5-turbo"
            };
            config.Providers.Add(provider);
            config.DefaultProvider = provider.Name;
            await _localSettingsService.SaveSettingAsync(KeyValues.LlmConfig, config);
            _cachedConfig = config;
        }

        return provider;
    }

    /// <summary>
    /// 流式对话实现
    /// </summary>
    public async IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(string prompt, string[]? models = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (models == null || models.Length == 0)
        {
            // 单个Provider流式响应
            await foreach (var chunk in ChatSingleStreamAsync(prompt, cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            // 批量Provider流式响应
            await foreach (var chunk in ChatBatchStreamAsync(prompt, models, cancellationToken))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// 单个Provider流式响应
    /// </summary>
    private async IAsyncEnumerable<StreamingChatChunk> ChatSingleStreamAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var providerConfig = await GetActiveProviderAsync();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 构建消息列表
            var messages = BuildMessagesWithPrompts(prompt, providerConfig);

            // 获取或创建Service实例
            var service = GetCachedService(providerConfig.Type);

            // 流式调用
            await foreach (var chunk in service.ChatStreamAsync(messages, providerConfig, cancellationToken))
            {
                // 创建新的chunk，添加provider信息
                yield return new StreamingChatChunk
                {
                    ProviderName = providerConfig.Type.ToString(),
                    DisplayName = providerConfig.Name,
                    Role = chunk.Role,
                    Content = chunk.Content,
                    IsEnd = chunk.IsEnd,
                    Error = chunk.Error
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            yield return new StreamingChatChunk
            {
                ProviderName = providerConfig.Type.ToString(),
                DisplayName = providerConfig.Name,
                Role = Enums.ChatRole.Assistant,
                Error = $"AI服务错误: {ex.Message}",
                IsEnd = true
            };
        }
    }

    /// <summary>
    /// 批量Provider流式响应
    /// </summary>
    private async IAsyncEnumerable<StreamingChatChunk> ChatBatchStreamAsync(string prompt, string[] modelNames, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync();

        // 查找匹配的Providers
        var targetProviders = new List<LlMProvider>();
        foreach (var modelName in modelNames)
        {
            var provider = config.Providers.FirstOrDefault(p =>
                p.Enabled && (p.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                               p.Model.Equals(modelName, StringComparison.OrdinalIgnoreCase)));

            if (provider != null && !targetProviders.Contains(provider))
            {
                targetProviders.Add(provider);
            }
        }

        if (targetProviders.Count == 0)
        {
            yield return new StreamingChatChunk
            {
                ProviderName = "System",
                DisplayName = "批量响应",
                Role = Enums.ChatRole.Assistant,
                Error = $"未找到匹配的已启用模型: {string.Join(", ", modelNames)}",
                IsEnd = true
            };
            yield break;
        }

        // 创建并发任务
        var tasks = targetProviders.Select(async provider =>
        {
            try
            {
                var messages = BuildMessagesWithPrompts(prompt, provider);
                var service = GetCachedService(provider.Type);
                return await service.ChatStreamAsync(messages, provider, cancellationToken).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<StreamingChatChunk>
                {
                    new StreamingChatChunk
                    {
                        ProviderName = provider.Type.ToString(),
                        DisplayName = provider.Name,
                        Role = Enums.ChatRole.Assistant,
                        Error = ex.Message,
                        IsEnd = true
                    }
                };
            }
        });

        // 等待所有任务开始
        var results = await Task.WhenAll(tasks);

        // 合并流式响应 - 按时间戳交错输出
        await foreach (var chunk in MergeStreams(results))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// 合并多个流式响应
    /// </summary>
    private static async IAsyncEnumerable<StreamingChatChunk> MergeStreams(List<List<StreamingChatChunk>> streams)
    {
        var enumerators = streams.Select(s => s.AsEnumerable()).ToList();
        var enumeratorsQueue = new Queue<IEnumerator<StreamingChatChunk>>(enumerators);

        while (enumeratorsQueue.Count > 0)
        {
            var currentEnumerator = enumeratorsQueue.Dequeue();
            if (currentEnumerator.MoveNext())
            {
                yield return currentEnumerator.Current;
                enumeratorsQueue.Enqueue(currentEnumerator);
            }
            else
            {
                currentEnumerator.Dispose();
            }
        }
    }

    /// <summary>
    /// 获取缓存的Service实例
    /// </summary>
    private ILlMService GetCachedService(LlMProviderType type)
    {
        if (_serviceCache.TryGetValue(type, out var service))
        {
            return service;
        }

        switch (type)
        {
            case LlMProviderType.OpenAiService:
                service = new OpenAiService();
                break;
            case LlMProviderType.LocalLlmService:
                service = new LocalLlmService();
                break;
            default:
                throw new NotSupportedException($"Provider type {type} is not supported");
        }

        _serviceCache[type] = service;
        return service;
    }

    /// <summary>
    /// 构建包含系统提示词和用户提示词的消息列表
    /// </summary>
    private List<ChatMessage> BuildMessagesWithPrompts(string userPrompt, LlMProvider config)
    {
        var messages = new List<ChatMessage>();

        // 添加系统提示词（如果有）
        if (!string.IsNullOrEmpty(config.SystemPrompt))
        {
            messages.Add(new ChatMessage(Enums.ChatRole.System, config.SystemPrompt));
        }

        // 处理用户提示词（添加前缀和后缀）
        var processedPrompt = config.ProcessUserMessage(userPrompt);
        messages.Add(new ChatMessage(Enums.ChatRole.User, processedPrompt));

        return messages;
    }

    // Helper methods for the main application
    public IReadOnlyList<LlMProviderType> GetAvailableProviders()
    {
        return Enum.GetValues<LlMProviderType>().ToList();
    }

    public ILlMService GetProviderService(LlMProviderType providerType)
    {
        return GetCachedService(providerType);
    }

    /// <summary>
    /// 获取所有已启用的Provider名称
    /// </summary>
    public async Task<string[]> GetEnabledProviderNamesAsync()
    {
        var config = await GetConfigAsync();
        return config.Providers
            .Where(p => p.Enabled)
            .Select(p => p.Name)
            .ToArray();
    }

    /// <summary>
    /// 清理缓存
    /// </summary>
    public void ClearCache()
    {
        foreach (var service in _serviceCache.Values)
        {
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _serviceCache.Clear();
    }

    // ILlMService implementation - just forward to ChatStreamAsync with prompt
    IAsyncEnumerable<StreamingChatChunk> Contracts.Services.ILlMService.ChatStreamAsync(List<ChatMessage> messages, LlMProvider config, CancellationToken cancellationToken)
    {
        // Convert messages back to prompt for simplicity
        var prompt = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));
        return ChatStreamAsync(prompt, null, cancellationToken);
    }
}