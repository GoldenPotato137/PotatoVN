using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.LLM;
using GalgameManager.WinApp.Base.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;

public class OpenAiService : ILlMService
{
    private readonly HttpClient _httpClient;
    private LlMProvider _config = null!;

    public OpenAiService()
    {
        _httpClient = Utils.GetDefaultHttpClient();
    }

    public async Task<ChatResponse> ChatLLMAsync(string prompt, params string[] models)
    {
        // This method should not be used directly
        // Configuration should be passed through the Initialize method
        throw new InvalidOperationException("OpenAiService requires configuration. Use Initialize() method first.");
    }

    public void Initialize(LlMProvider config)
    {
        _config = config;
    }

    public async Task<ChatResponse> ChatAsync(List<ChatMessage> messages)
    {
        if (_config == null)
            throw new InvalidOperationException("OpenAiService not initialized. Call Initialize() first.");

        var (apiKey, baseUrl, model) = ParseConfig();

        var requestBody = new
        {
            model,
            messages = messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToList()
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
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
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            var messageContent = json["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;

            stopwatch.Stop();
            return ChatResponse.CreateSuccess(
                _config.Type,
                _config.Name,
                ChatRole.Assistant,
                messageContent,
                stopwatch.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ChatResponse.CreateFailure(
                _config.Type,
                _config.Name,
                ex.Message,
                stopwatch.ElapsedMilliseconds
            );
        }
    }

    private (string apiKey, string baseUrl, string model) ParseConfig()
    {
        var apiKey = _config.ApiKey;
        var baseUrl = _config.BaseUrl;
        var model = _config.Model;

        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return (apiKey, baseUrl, model);
    }
}