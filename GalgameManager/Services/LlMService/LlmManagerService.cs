using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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

    private async Task<List<LlMProvider>> GetAllProvidersAsync()
    {
        var config = await GetConfigAsync();

        // Ensure we have at least one provider
        if (config.Providers.Count == 0)
        {
            await GetActiveProviderAsync(); // This will create a default provider
            return (await GetConfigAsync()).Providers;
        }

        return config.Providers;
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
                Type = "OpenAiService",
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

            // 创建服务实例
            var service = CreateServiceInstance(providerConfig.Type);

            // 初始化服务配置
            var initializeMethod = service.GetType().GetMethod("Initialize");
            initializeMethod?.Invoke(service, new object[] { providerConfig });

            // 构建消息列表（包含系统提示词）
            var messages = BuildMessagesWithPrompts(prompt, providerConfig);

            // 调用服务的ChatAsync方法
            var chatMethod = service.GetType().GetMethod("ChatAsync");
            var response = await (Task<ChatResponse>)chatMethod?.Invoke(service, new object[] { messages })!;

            stopwatch.Stop();
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ChatResponse.CreateFailure(
                "Unknown",
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
                "System",
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
                // 创建服务实例
                var service = CreateServiceInstance(provider.Type);

                // 初始化服务配置
                var initializeMethod = service.GetType().GetMethod("Initialize");
                initializeMethod?.Invoke(service, new object[] { provider });

                // 构建消息列表（包含系统提示词）
                var messages = BuildMessagesWithPrompts(prompt, provider);

                // 调用服务的ChatAsync方法
                var chatMethod = service.GetType().GetMethod("ChatAsync");
                var response = await (Task<ChatResponse>)chatMethod?.Invoke(service, new object[] { messages })!;

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
    /// 创建LLM服务实例
    /// </summary>
    /// <param name="serviceType">服务类型名称</param>
    /// <returns>服务实例</returns>
    private object CreateServiceInstance(string serviceType)
    {
        return serviceType switch
        {
            "OpenAiService" => new OpenAiService(),
            "LocalLlmService" => new LocalLlmService(),
            _ => CreateDynamicServiceInstance(serviceType)
        };
    }

    /// <summary>
    /// 动态创建LLM服务实例
    /// </summary>
    /// <param name="serviceType">服务类型名称</param>
    /// <returns>服务实例</returns>
    private object CreateDynamicServiceInstance(string serviceType)
    {
        // Get current assembly
        var assembly = Assembly.GetExecutingAssembly();

        // Find the type
        var type = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == serviceType);

        if (type == null)
        {
            throw new TypeLoadException($"Provider type {serviceType} not found");
        }

        // Create instance
        var service = Activator.CreateInstance(type);
        if (service == null)
        {
            throw new InvalidOperationException($"Failed to create instance of {serviceType}");
        }

        return service;
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
    public IReadOnlyList<string> GetAvailableProviders()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetTypes()
            .Where(t => t.IsClass &&
                       !t.IsAbstract &&
                       t.GetMethods().Any(m => m.Name == "ChatAsync") &&
                       t.GetMethods().Any(m => m.Name == "Initialize") &&
                       t.Name != nameof(LlmManagerService))
            .Select(t => t.Name)
            .ToList();
    }

    public object GetProviderService(string typeName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var type = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == typeName);

        if (type == null)
        {
            throw new ArgumentException($"Provider {typeName} not found");
        }

        return Activator.CreateInstance(type)!;
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
