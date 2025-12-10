using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models.LLM;

namespace GalgameManager.Services;

public class LocalLlmService : ILlMService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _providerName;
    private readonly LlMProviderType _providerType;
    private readonly bool _isOllama;

    public LocalLlmService(LlMProvider config)
    {
        _httpClient = new HttpClient(); // Local requests don't need the default proxy/headers usually
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Long timeout for local inference

        var url = config.BaseUrl;
        if (string.IsNullOrEmpty(url)) url = "http://localhost:11434";
        if (!url.EndsWith("/")) url += "/";
        
        _baseUrl = url;
        _model = config.Model;
        _providerName = config.Name;
        _providerType = config.Type;
        _isOllama = url.Contains("11434"); // Simple heuristic
    }

    public async IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(IEnumerable<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = await ResolveModelAsync();
        var endpoint = _isOllama ? "api/chat" : "v1/chat/completions";
        var request = CreateRequest(endpoint, messages, model, true);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        var buffer = new char[8192];
        var currentLine = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (charsRead == 0) break;

            for (int i = 0; i < charsRead; i++)
            {
                var c = buffer[i];
                if (c == '\n')
                {
                    var line = currentLine.ToString().Trim();
                    currentLine.Clear();
                    if (string.IsNullOrEmpty(line)) continue;

                    StreamingChatChunk? chunk;
                    if (_isOllama)
                        chunk = ParseOllamaChunk(line);
                    else
                        chunk = ParseOpenAiChunk(line);

                    if (chunk.HasValue) yield return chunk.Value;
                }
                else
                {
                    currentLine.Append(c);
                }
            }
        }
    }

    public async Task<string> ChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var model = await ResolveModelAsync();
        var endpoint = _isOllama ? "api/chat" : "v1/chat/completions";
        var request = CreateRequest(endpoint, messages, model, false);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var jsonDoc = JsonDocument.Parse(responseString);
        var root = jsonDoc.RootElement;

        if (_isOllama)
        {
            if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
                return content.GetString() ?? "";
        }
        else
        {
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                    return content.GetString() ?? "";
            }
        }

        return "";
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var endpoint = _isOllama ? "api/tags" : "v1/models";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response = await _httpClient.GetAsync(_baseUrl + endpoint, cts.Token);
            return response.IsSuccessStatusCode;
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
            var endpoint = _isOllama ? "api/tags" : "v1/models";
            using var response = await _httpClient.GetAsync(_baseUrl + endpoint);
            if (!response.IsSuccessStatusCode) return Array.Empty<string>();

            var content = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(content);
            
            if (_isOllama)
            {
                if (jsonDoc.RootElement.TryGetProperty("models", out var models))
                    return models.EnumerateArray()
                        .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                        .Where(x => x != null)
                        .Cast<string>()
                        .ToArray();
            }
            else
            {
                if (jsonDoc.RootElement.TryGetProperty("data", out var data))
                    return data.EnumerateArray()
                        .Select(m => m.TryGetProperty("id", out var id) ? id.GetString() : null)
                        .Where(x => x != null)
                        .Cast<string>()
                        .ToArray();
            }
        }
        catch { }
        return Array.Empty<string>();
    }

    private async Task<string> ResolveModelAsync()
    {
        if (!string.IsNullOrEmpty(_model)) return _model;
        var models = await GetModelsAsync();
        return models.FirstOrDefault() ?? "llama2";
    }

    private HttpRequestMessage CreateRequest(string endpoint, IEnumerable<ChatMessage> messages, string model, bool stream)
    {
        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role.ToString().ToLower(), content = m.Content }).ToArray(),
            stream
        };

        return new HttpRequestMessage(HttpMethod.Post, _baseUrl + endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private StreamingChatChunk? ParseOllamaChunk(string line)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(line);
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
            {
                var isDone = root.TryGetProperty("done", out var done) && done.GetBoolean();
                return new StreamingChatChunk
                {
                    ProviderName = _providerType.ToString(),
                    DisplayName = _providerName,
                    Role = ChatRole.Assistant,
                    Content = content.GetString() ?? "",
                    IsEnd = isDone
                };
            }
        }
        catch { }
        return null;
    }

    private StreamingChatChunk? ParseOpenAiChunk(string line)
    {
        if (!line.StartsWith("data: ")) return null;
        var data = line.Substring(6);
        if (data == "[DONE]") return new StreamingChatChunk { IsEnd = true, ProviderName = _providerType.ToString(), DisplayName = _providerName };

        try
        {
            using var jsonDoc = JsonDocument.Parse(data);
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content))
                {
                    return new StreamingChatChunk
                    {
                        ProviderName = _providerType.ToString(),
                        DisplayName = _providerName,
                        Role = ChatRole.Assistant,
                        Content = content.GetString() ?? "",
                        IsEnd = false
                    };
                }
            }
        }
        catch { }
        return null;
    }
}