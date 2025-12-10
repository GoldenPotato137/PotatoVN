using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.LLM;

namespace GalgameManager.Services;

public class OpenAiService : ILlMService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _providerName;
    private readonly LlMProviderType _providerType;

    private const string ChatEndpoint = "chat/completions";
    private const string ModelsEndpoint = "models";

    public OpenAiService(LlMProvider config)
    {
        _httpClient = Utils.GetDefaultHttpClient();
        _apiKey = config.ApiKey;
        _baseUrl = config.BaseUrl.EndsWith("/") ? config.BaseUrl : config.BaseUrl + "/";
        _model = config.Model;
        _providerName = config.Name;
        _providerType = config.Type;
    }

    public async IAsyncEnumerable<StreamingChatChunk> ChatStreamAsync(IEnumerable<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(ChatEndpoint, messages, true);
        
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        // Reusing buffer to minimize allocations
        var buffer = new char[8192];
        var currentLine = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var charsRead = await reader.ReadAsync(buffer, cancellationToken);
            if (charsRead == 0) break;

            for (var i = 0; i < charsRead; i++)
            {
                var c = buffer[i];
                if (c == '\n')
                {
                    var line = currentLine.ToString().Trim();
                    currentLine.Clear();

                    if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

                    var data = line.Substring(6);
                    if (data == "[DONE]") break;

                    StreamingChatChunk? chunk = ParseStreamChunk(data);
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
        var request = CreateRequest(ChatEndpoint, messages, false);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var jsonDoc = JsonDocument.Parse(responseString);
        
        if (jsonDoc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message) && 
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? "";
            }
        }

        return string.Empty;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + ModelsEndpoint);
            AddAuthorization(request);
            
            // Short timeout for connection test
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await _httpClient.SendAsync(request, cts.Token);
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
            var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + ModelsEndpoint);
            AddAuthorization(request);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Array.Empty<string>();

            var content = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(content);
            
            if (jsonDoc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                return data.EnumerateArray()
                    .Select(x => x.TryGetProperty("id", out var id) ? id.GetString() : null)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Cast<string>()
                    .ToArray();
            }
        }
        catch
        {
            // Ignore errors for model fetching
        }
        return Array.Empty<string>();
    }

    private HttpRequestMessage CreateRequest(string endpoint, IEnumerable<ChatMessage> messages, bool stream)
    {
        var payload = new
        {
            model = _model,
            messages = messages.Select(m => new { role = m.Role.ToString().ToLower(), content = m.Content }).ToArray(),
            stream
        };

        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        
        AddAuthorization(request);
        return request;
    }

    private void AddAuthorization(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    private StreamingChatChunk? ParseStreamChunk(string data)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(data);
            var root = jsonDoc.RootElement;
            
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta) && 
                    delta.TryGetProperty("content", out var content))
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
        catch
        {
            // Invalid JSON chunk, ignore
        }
        return null;
    }
}
