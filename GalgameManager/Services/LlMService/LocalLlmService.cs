using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models.LLM;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;

public class LocalLlmService : ILlMService
{
    private readonly HttpClient _httpClient;

    public LocalLlmService()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// 流式对话实现
    /// </summary>
    public async IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(List<ChatMessage> messages, LlMProvider config, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (endpoint, model) = ParseConfig(config);

        // If no model specified, try to use a default
        if (string.IsNullOrEmpty(model))
        {
            model = await GetDefaultModel(endpoint) ?? "llama2";
        }

        // Try to detect the local LLM service type and format request accordingly
        if (endpoint.Contains("11434"))
        {
            // Ollama API format
            await foreach (var chunk in ChatStreamWithOllama(messages, endpoint, model, config, cancellationToken))
            {
                yield return chunk;
            }
        }
        else if (endpoint.Contains("api"))
        {
            // Generic OpenAI-compatible API format
            await foreach (var chunk in ChatStreamWithOpenAiCompatible(messages, endpoint, model, config, cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            // Command line doesn't support true streaming, return error
            yield return new StreamingChatChunk
            {
                ProviderName = config.Type.ToString(),
                DisplayName = config.Name,
                Role = ChatRole.Assistant,
                Error = "命令行模式不支持流式响应，请使用HTTP API",
                IsEnd = true
            };
        }
    }

    private (string endpoint, string model) ParseConfig(LlMProvider config)
    {
        // For local LLM, BaseUrl should contain the endpoint (e.g., http://localhost:11434)
        var endpoint = config.BaseUrl;
        var model = config.Model;

        // Set default endpoint if not provided
        if (string.IsNullOrEmpty(endpoint))
        {
            endpoint = "http://localhost:11434"; // Default to Ollama endpoint
        }

        if (!endpoint.EndsWith("/")) endpoint += "/";
        return (endpoint, model);
    }

    private async IAsyncEnumerable<StreamingChatChunk> ChatStreamWithOllama(List<ChatMessage> messages, string endpoint, string model, LlMProvider config, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model,
            messages = messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToList(),
            stream = true
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _httpClient.PostAsync($"{endpoint}api/chat", content, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            var buffer = new char[4096];
            var currentLine = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (charsRead == 0) break;

                for (int i = 0; i < charsRead; i++)
                {
                    currentLine.Append(buffer[i]);
                    if (buffer[i] == '\n')
                    {
                        var line = currentLine.ToString().Trim();
                        currentLine.Clear();

                        if (!string.IsNullOrEmpty(line))
                        {
                            try
                            {
                                using var jsonDoc = JsonDocument.Parse(line);
                                var root = jsonDoc.RootElement;

                                if (root.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentElem))
                                {
                                    yield return new StreamingChatChunk
                                    {
                                        ProviderName = config.Type.ToString(),
                                        DisplayName = config.Name,
                                        Role = ChatRole.Assistant,
                                        Content = (contentElem.GetString() ?? "").AsMemory(),
                                        IsEnd = root.TryGetProperty("done", out var done) && done.GetBoolean()
                                    };
                                }
                            }
                            catch (JsonException)
                            {
                                // 忽略无效的JSON行
                                continue;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            yield return new StreamingChatChunk
            {
                ProviderName = config.Type.ToString(),
                DisplayName = config.Name,
                Role = ChatRole.Assistant,
                Error = $"Ollama服务错误: {ex.Message}",
                IsEnd = true
            };
        }
    }

    private async IAsyncEnumerable<StreamingChatChunk> ChatStreamWithOpenAiCompatible(List<ChatMessage> messages, string endpoint, string model, LlMProvider config, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model,
            messages = messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToList(),
            stream = true
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _httpClient.PostAsync($"{endpoint}v1/chat/completions", content, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            var buffer = new char[4096];
            var currentLine = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (charsRead == 0) break;

                for (int i = 0; i < charsRead; i++)
                {
                    currentLine.Append(buffer[i]);
                    if (buffer[i] == '\n')
                    {
                        var line = currentLine.ToString().Trim();
                        currentLine.Clear();

                        if (line.StartsWith("data: "))
                        {
                            var data = line.Substring(6);
                            if (data == "[DONE]")
                            {
                                yield return new StreamingChatChunk
                                {
                                    ProviderName = config.Type.ToString(),
                                    DisplayName = config.Name,
                                    Role = ChatRole.Assistant,
                                    Content = ReadOnlyMemory<char>.Empty,
                                    IsEnd = true
                                };
                                yield break;
                            }

                            try
                            {
                                using var jsonDoc = JsonDocument.Parse(data);
                                var root = jsonDoc.RootElement;

                                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                                {
                                    var choice = choices[0];
                                    if (choice.TryGetProperty("delta", out var delta))
                                    {
                                        var contentChunk = delta.TryGetProperty("content", out var contentElem) ? contentElem.GetString() : "";

                                        yield return new StreamingChatChunk
                                        {
                                            ProviderName = config.Type.ToString(),
                                            DisplayName = config.Name,
                                            Role = ChatRole.Assistant,
                                            Content = (contentChunk ?? "").AsMemory(),
                                            IsEnd = false
                                        };
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                // 忽略无效的JSON行
                                continue;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            yield return new StreamingChatChunk
            {
                ProviderName = config.Type.ToString(),
                DisplayName = config.Name,
                Role = ChatRole.Assistant,
                Error = $"本地API服务错误: {ex.Message}",
                IsEnd = true
            };
        }
    }

    private async Task<string?> GetDefaultModel(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{endpoint}api/tags");
            if (!response.IsSuccessStatusCode) return null;

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            var firstModel = json["models"]?.FirstOrDefault()?["name"]?.ToString();
            return firstModel;
        }
        catch
        {
            return null;
        }
    }
}