using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.LLM;

namespace GalgameManager.Services;

public class OpenAiService : ILlMService
{
    private readonly HttpClient _httpClient;

    public OpenAiService()
    {
        _httpClient = Utils.GetDefaultHttpClient();
    }

    /// <summary>
    /// 流式对话实现
    /// </summary>
    public async IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(List<ChatMessage> messages, LlMProvider config, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (apiKey, baseUrl, model) = ParseConfig(config);

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

        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "chat/completions")
        {
            Content = content
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
                Error = $"OpenAI服务错误: {ex.Message}",
                IsEnd = true
            };
        }
    }

    private (string apiKey, string baseUrl, string model) ParseConfig(LlMProvider config)
    {
        var apiKey = config.ApiKey;
        var baseUrl = config.BaseUrl;
        var model = config.Model;

        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return (apiKey, baseUrl, model);
    }
}