using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models.LLM;
using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.Services;

public class LlmManagerService : ILlMService
{
    private readonly ILocalSettingsService _localSettingsService;
    private LlMConfig? _cachedConfig;

    public LlmManagerService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    private async Task<LlMConfig> GetConfigAsync()
    {
        _cachedConfig ??= await _localSettingsService.ReadSettingAsync<LlMConfig>(KeyValues.LlMConfig) ?? new LlMConfig();
        return _cachedConfig;
    }

    private async Task<LlMProvider> GetActiveProviderAsync()
    {
        var config = await GetConfigAsync();

        // Find the default provider
        LlMProvider? provider = null;

        if (!string.IsNullOrEmpty(config.DefaultProvider))
        {
            provider = config.Providers.FirstOrDefault(p => p.Name == config.DefaultProvider);
        }

        // If no default found or not specified, use the first provider
        provider ??= config.Providers.FirstOrDefault();

        if (provider == null)
        {
            // Create a default one if none exists
            provider = new LlMProvider
            {
                Name = "Default OpenAI",
                Type = LlMProviderType.OpenAiService,
                BaseUrl = "https://api.openai.com/v1/",
                Model = "gpt-3.5-turbo"
            };
            config.Providers.Add(provider);
            config.DefaultProvider = provider.Name;
            await _localSettingsService.SaveSettingAsync(KeyValues.LlMConfig, config);
            _cachedConfig = config;
        }

        return provider;
    }

    // ILlMService implementation
    public async Task<ChatResponse> ChatLLMAsync(string prompt, params string[] models)
    {
        if (models == null || models.Length == 0)
        {
            // 单个模型调用 - 使用默认Provider
            return await ChatWithSingleProvider(prompt);
        }
        else
        {
            // 批量模型调用
            return await ChatWithMultipleProviders(prompt, models);
        }
    }

    /// <summary>
    /// 使用单个Provider进行对话
    /// </summary>
    private async Task<ChatResponse> ChatWithSingleProvider(string prompt)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var providerConfig = await GetActiveProviderAsync();

            // 构建消息列表（包含系统提示词）
            var messages = BuildMessagesWithPrompts(prompt, providerConfig);

            // 直接调用具体服务的ChatWithConfigAsync方法
            var response = await CallServiceWithConfig(providerConfig.Type, messages, providerConfig);

            stopwatch.Stop();
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ChatResponse.CreateFailure(
                LlMProviderType.OpenAiService,
                "AI服务",
                $"AI服务错误: {ex.Message}",
                stopwatch.ElapsedMilliseconds
            );
        }
    }

    /// <summary>
    /// 使用多个Provider进行批量对话
    /// </summary>
    private async Task<ChatResponse> ChatWithMultipleProviders(string prompt, string[] modelNames)
    {
        var config = await GetConfigAsync();
        var stopwatch = Stopwatch.StartNew();

        // 根据模型名称查找匹配的Providers
        var targetProviders = new List<LlMProvider>();

        foreach (var modelName in modelNames)
        {
            // 优先按Name匹配
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
            stopwatch.Stop();
            return ChatResponse.CreateFailure(
                LlMProviderType.LocalLlmService,
                "批量调用",
                $"未找到匹配的已启用模型: {string.Join(", ", modelNames)}",
                stopwatch.ElapsedMilliseconds
            );
        }

        // 创建批量任务
        var tasks = targetProviders.Select(async provider =>
        {
            var providerStopwatch = Stopwatch.StartNew();
            try
            {
                // 构建消息列表（包含系统提示词）
                var messages = BuildMessagesWithPrompts(prompt, provider);

                // 直接调用具体服务的ChatWithConfigAsync方法
                var response = await CallServiceWithConfig(provider.Type, messages, provider);

                providerStopwatch.Stop();
                return response;
            }
            catch (Exception ex)
            {
                providerStopwatch.Stop();
                return ChatResponse.CreateFailure(
                    provider.Type,
                    provider.Name,
                    ex.Message,
                    providerStopwatch.ElapsedMilliseconds
                );
            }
        });

        // 等待所有任务完成
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();
        return ChatResponse.CreateBatch(results);
    }

    /// <summary>
    /// 调用具体服务的ChatWithConfigAsync方法
    /// </summary>
    /// <param name="providerType">服务提供者类型</param>
    /// <param name="messages">消息列表</param>
    /// <param name="config">提供者配置</param>
    /// <returns>聊天响应</returns>
    private async Task<ChatResponse> CallServiceWithConfig(LlMProviderType providerType, List<ChatMessage> messages, LlMProvider config)
    {
        return providerType switch
        {
            LlMProviderType.OpenAiService => await new OpenAiService().ChatWithConfigAsync(messages, config),
            LlMProviderType.LocalLlmService => await new LocalLlmService().ChatWithConfigAsync(messages, config),
            _ => throw new NotSupportedException($"Provider type {providerType} is not supported")
        };
    }

    /// <summary>
    /// 构建包含系统提示词和用户提示词的消息列表
    /// </summary>
    /// <param name="userPrompt">原始用户提示</param>
    /// <param name="config">Provider配置</param>
    /// <returns>完整的消息列表</returns>
    private List<ChatMessage> BuildMessagesWithPrompts(string userPrompt, LlMProvider config)
    {
        var messages = new List<ChatMessage>();

        // 添加系统提示词（如果有）
        if (!string.IsNullOrEmpty(config.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, config.SystemPrompt));
        }

        // 处理用户提示词（添加前缀和后缀）
        var processedPrompt = config.ProcessUserMessage(userPrompt);
        messages.Add(new ChatMessage(ChatRole.User, processedPrompt));

        return messages;
    }

    // Helper methods for the main application
    public IReadOnlyList<LlMProviderType> GetAvailableProviders()
    {
        return Enum.GetValues<LlMProviderType>().ToList();
    }

    public object GetProviderService(LlMProviderType providerType)
    {
        return providerType switch
        {
            LlMProviderType.OpenAiService => new OpenAiService(),
            LlMProviderType.LocalLlmService => new LocalLlmService(),
            _ => throw new NotSupportedException($"Provider type {providerType} is not supported")
        };
    }

    /// <summary>
    /// 获取所有已启用的Provider名称
    /// </summary>
    /// <returns>已启用的Provider名称列表</returns>
    public async Task<string[]> GetEnabledProviderNamesAsync()
    {
        var config = await GetConfigAsync();
        return config.Providers
            .Where(p => p.Enabled)
            .Select(p => p.Name)
            .ToArray();
    }
}
