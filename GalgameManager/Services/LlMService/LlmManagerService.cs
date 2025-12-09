using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
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
        _cachedConfig ??= await _localSettingsService.ReadSettingAsync<LlMConfig>(KeyValues.LlmConfig) ?? new LlMConfig();
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
            await _localSettingsService.SaveSettingAsync(KeyValues.LlmConfig, config);
            _cachedConfig = config;
        }

        return provider;
    }

    // ILlMService implementation
    public async Task<ChatMessage> ChatAsync(string prompt)
    {
        try
        {
            var providerConfig = await GetActiveProviderAsync();

            switch (providerConfig.Type)
            {
                case "OpenAiService":
                    return await ChatWithOpenAi(prompt, providerConfig);
                case "LocalLlmService":
                    return await ChatWithLocalLlm(prompt, providerConfig);
                default:
                    // Try to find and instantiate the service dynamically
                    return await ChatWithDynamicService(prompt, providerConfig);
            }
        }
        catch (Exception ex)
        {
            return new ChatMessage(ChatRole.Assistant, $"AI服务错误: {ex.Message}");
        }
    }

    private async Task<ChatMessage> ChatWithOpenAi(string prompt, LlMProvider config)
    {
        var service = new OpenAiService();
        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        return await service.ChatAsync(messages, config);
    }

    private async Task<ChatMessage> ChatWithLocalLlm(string prompt, LlMProvider config)
    {
        var service = new LocalLlmService();
        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        return await service.ChatAsync(messages, config);
    }

    private async Task<ChatMessage> ChatWithDynamicService(string prompt, LlMProvider config)
    {
        // Get current assembly
        var assembly = Assembly.GetExecutingAssembly();

        // Find the type
        var type = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == config.Type);

        if (type == null)
        {
            throw new TypeLoadException($"Provider type {config.Type} not found");
        }

        // Create instance
        if (Activator.CreateInstance(type) is not object service)
        {
            throw new InvalidOperationException($"Failed to create instance of {config.Type}");
        }

        // Find the ChatAsync method
        var method = type.GetMethod("ChatAsync", new[] { typeof(List<ChatMessage>), typeof(LlMProvider) });
        if (method == null)
        {
            throw new InvalidOperationException($"Provider {config.Type} does not have ChatAsync method");
        }

        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        return await (Task<ChatMessage>)method.Invoke(service, new object[] { messages, config });
    }

    // Helper methods for the main application
    public async Task<IReadOnlyList<string>> GetAvailableProviders()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetTypes()
            .Where(t => t.IsClass &&
                       !t.IsAbstract &&
                       typeof(ILlMService).IsAssignableFrom(t) &&
                       t.Name != nameof(LlmManagerService))
            .Select(t => t.Name)
            .ToList();
    }

    public async Task<ILlMService> GetProviderService(string typeName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var type = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == typeName);

        if (type == null)
        {
            throw new ArgumentException($"Provider {typeName} not found");
        }

        return (ILlMService)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// 批量Chat - 将同一个prompt发送给所有已启用的LLM
    /// </summary>
    public async Task<BatchChatResponse> BatchChatAsync(string prompt)
    {
        var response = new BatchChatResponse();
        var config = await GetConfigAsync();

        // Only process enabled providers
        var enabledProviders = config.Providers.Where(p => p.Enabled).ToList();

        if (enabledProviders.Count == 0)
        {
            response.Errors.Add(new ChatError
            {
                ProviderName = "System",
                DisplayName = "系统",
                Error = "没有启用的LLM Provider"
            });
            return response;
        }

        // Create tasks for all enabled providers
        var tasks = enabledProviders.Select(async provider =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var chatResponse = await ChatWithProvider(prompt, provider);
                stopwatch.Stop();

                response.Successes.Add(new ChatResult
                {
                    ProviderName = provider.Type,
                    DisplayName = provider.Name,
                    Message = chatResponse,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.Errors.Add(new ChatError
                {
                    ProviderName = provider.Type,
                    DisplayName = provider.Name,
                    Error = ex.Message,
                    Exception = ex
                });
            }
        });

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        return response;
    }

    private async Task<ChatMessage> ChatWithProvider(string prompt, LlMProvider provider)
    {
        switch (provider.Type)
        {
            case "OpenAiService":
                return await ChatWithOpenAi(prompt, provider);
            case "LocalLlmService":
                return await ChatWithLocalLlm(prompt, provider);
            default:
                return await ChatWithDynamicService(prompt, provider);
        }
    }
}