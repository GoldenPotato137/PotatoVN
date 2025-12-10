using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Models.LLM;

namespace GalgameManager.Services;

/// <summary>
/// LLM Service Manager
/// Central entry point for all LLM interactions.
/// </summary>
public class LlmManagerService : ILlMService
{
    private readonly ILocalSettingsService _localSettingsService;
    private LlMConfig? _cachedConfig;
    private readonly Dictionary<string, ILlMService> _serviceCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    // Default timeout for all LLM requests
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public LlmManagerService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    #region Public API

    /// <summary>
    /// Stream chat with one or multiple models.
    /// </summary>
    public async IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(string prompt, string[]? models = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultTimeout);
        
        var targetProviders = await ResolveProvidersAsync(models);
        
        if (targetProviders.Count == 0)
        {
            yield return ErrorChunk("System", "System", "No active providers found.");
            yield break;
        }

        if (targetProviders.Count == 1)
        {
            await foreach (var chunk in ChatSingleStreamAsync(prompt, targetProviders[0], cts.Token))
                yield return chunk;
        }
        else
        {
            await foreach (var chunk in ChatBatchStreamAsync(prompt, targetProviders, cts.Token))
                yield return chunk;
        }
    }

    /// <summary>
    /// Standard non-streaming chat (single active provider).
    /// </summary>
    public async Task<string> ChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultTimeout);

        try
        {
            var provider = await GetActiveProviderAsync();
            var service = await GetServiceInstanceAsync(provider);
            return await service.ChatAsync(messages, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return "Error: Request timed out.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var provider = await GetActiveProviderAsync();
            var service = await GetServiceInstanceAsync(provider);
            return await service.TestConnectionAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<string[]> GetModelsAsync()
    {
        try
        {
            var provider = await GetActiveProviderAsync();
            var service = await GetServiceInstanceAsync(provider);
            return await service.GetModelsAsync();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // ILlMService explicit implementation for interface compliance
    IAsyncEnumerable<StreamingChatChunk> ILlMService.ChatStreamAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        return ChatStreamAsyncWrapper(messages, cancellationToken);
    }
    
    private async IAsyncEnumerable<StreamingChatChunk> ChatStreamAsyncWrapper(IEnumerable<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultTimeout);

        var provider = await GetActiveProviderAsync();
        await foreach (var chunk in ChatSingleStreamInternalAsync(messages, provider, cts.Token))
            yield return chunk;
    }

    #endregion

    #region Internal Logic

    private async IAsyncEnumerable<StreamingChatChunk> ChatSingleStreamAsync(string prompt, LlMProvider provider, [EnumeratorCancellation] CancellationToken token)
    {
        var messages = BuildMessages(prompt, provider);
        await foreach (var chunk in ChatSingleStreamInternalAsync(messages, provider, token))
            yield return chunk;
    }

    private async IAsyncEnumerable<StreamingChatChunk> ChatSingleStreamInternalAsync(IEnumerable<ChatMessage> messages, LlMProvider provider, [EnumeratorCancellation] CancellationToken token)
    {
        ILlMService service;
        try
        {
            service = await GetServiceInstanceAsync(provider);
        }
        catch (Exception ex)
        {
            yield return ErrorChunk(provider.Type.ToString(), provider.Name, ex.Message);
            yield break;
        }

        IAsyncEnumerator<StreamingChatChunk>? enumerator = null;
        try
        {
            enumerator = service.ChatStreamAsync(messages, token).GetAsyncEnumerator(token);
            while (true)
            {
                // MoveNextAsync with timeout handling implicitly via token
                if (!await enumerator.MoveNextAsync()) break;
                
                var chunk = enumerator.Current;
                yield return chunk with { ProviderName = provider.Type.ToString(), DisplayName = provider.Name };
            }
        }
        catch (OperationCanceledException)
        {
            yield return ErrorChunk(provider.Type.ToString(), provider.Name, "Request timed out.");
        }
        catch (Exception ex)
        {
            yield return ErrorChunk(provider.Type.ToString(), provider.Name, ex.Message);
        }
        finally
        {
            if (enumerator != null) await enumerator.DisposeAsync();
        }
    }

    private async IAsyncEnumerable<StreamingChatChunk> ChatBatchStreamAsync(string prompt, List<LlMProvider> providers, [EnumeratorCancellation] CancellationToken token)
    {
        var activeEnumerators = new List<(IAsyncEnumerator<StreamingChatChunk> Enumerator, LlMProvider Provider, Task<bool> NextTask)>();

        // Initialize all streams
        foreach (var provider in providers)
        {
            try
            {
                var messages = BuildMessages(prompt, provider);
                var service = await GetServiceInstanceAsync(provider);
                var enumerator = service.ChatStreamAsync(messages, token).GetAsyncEnumerator(token);
                activeEnumerators.Add((enumerator, provider, enumerator.MoveNextAsync().AsTask()));
            }
            catch (Exception ex)
            {
                yield return ErrorChunk(provider.Type.ToString(), provider.Name, $"Init failed: {ex.Message}");
            }
        }

        try
        {
            while (activeEnumerators.Count > 0)
            {
                // Wait for any provider to have data
                var completedTask = await Task.WhenAny(activeEnumerators.Select(x => x.NextTask));
                var index = activeEnumerators.FindIndex(x => x.NextTask == completedTask);
                
                if (index == -1) continue; // Should not happen
                
                var (enumerator, provider, _) = activeEnumerators[index];
                
                bool hasMore = false;
                try
                {
                    hasMore = await completedTask;
                }
                catch (OperationCanceledException)
                {
                    yield return ErrorChunk(provider.Type.ToString(), provider.Name, "Timeout");
                }
                catch (Exception ex)
                {
                    yield return ErrorChunk(provider.Type.ToString(), provider.Name, ex.Message);
                }

                if (hasMore)
                {
                    yield return enumerator.Current with { ProviderName = provider.Type.ToString(), DisplayName = provider.Name };
                    // Queue next read
                    activeEnumerators[index] = (enumerator, provider, enumerator.MoveNextAsync().AsTask());
                }
                else
                {
                    // Stream finished
                    await enumerator.DisposeAsync();
                    activeEnumerators.RemoveAt(index);
                }
            }
        }
        finally
        {
            // Clean up any remaining
            foreach (var (enumerator, _, _) in activeEnumerators)
                await enumerator.DisposeAsync();
        }
    }

    #endregion

    #region Helpers

    private async Task<List<LlMProvider>> ResolveProvidersAsync(string[]? modelNames)
    {
        var config = await GetConfigAsync();
        if (modelNames == null || modelNames.Length == 0)
        {
            return new List<LlMProvider> { await GetActiveProviderAsync() };
        }

        return config.Providers
            .Where(p => p.Enabled && modelNames.Any(m => 
                m.Equals(p.Name, StringComparison.OrdinalIgnoreCase) || 
                m.Equals(p.Model, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(p => p.Name)
            .ToList();
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
                Type = LlMProviderType.OpenAI,
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

    private async Task<ILlMService> GetServiceInstanceAsync(LlMProvider provider)
    {
        if (_serviceCache.TryGetValue(provider.Name, out var service)) return service;

        await _cacheLock.WaitAsync();
        try
        {
            if (_serviceCache.TryGetValue(provider.Name, out service)) return service;

            service = provider.Type switch
            {
                LlMProviderType.OpenAI => new OpenAiService(provider),
                LlMProviderType.Local => new LocalLlmService(provider),
                _ => throw new NotSupportedException($"Unknown provider type: {provider.Type}")
            };

            _serviceCache[provider.Name] = service;
            return service;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private List<ChatMessage> BuildMessages(string userPrompt, LlMProvider config)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(config.SystemPrompt))
            messages.Add(new ChatMessage(Enums.ChatRole.System, config.SystemPrompt));

        var processed = config.ProcessUserMessage(userPrompt);
        messages.Add(new ChatMessage(Enums.ChatRole.User, processed));
        return messages;
    }

    private StreamingChatChunk ErrorChunk(string providerType, string providerName, string error) =>
        new()
        {
            ProviderName = providerType,
            DisplayName = providerName,
            Role = Enums.ChatRole.Assistant,
            Error = error,
            IsEnd = true
        };
        
    public void ClearCache()
    {
        _cacheLock.Wait();
        try
        {
            _serviceCache.Clear();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    #endregion
}