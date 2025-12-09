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

    public OpenAiService()
    {
        _httpClient = Utils.GetDefaultHttpClient();
    }

    public async Task<ChatResponse> ChatLLMAsync(string prompt, params string[] models)
    {
        // This method is called from LlmManagerService with direct config passing
        throw new NotImplementedException("Use ChatWithConfigAsync method instead.");
    }

    /// <summary>
    /// 使用配置直接进行对话
    /// </summary>
    public async Task<ChatResponse> ChatWithConfigAsync(List<ChatMessage> messages, LlMProvider config)
    {
        var (apiKey, baseUrl, model) = ParseConfig(config);

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
                config.Type,
                config.Name,
                ChatRole.Assistant,
                messageContent,
                stopwatch.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ChatResponse.CreateFailure(
                config.Type,
                config.Name,
                ex.Message,
                stopwatch.ElapsedMilliseconds
            );
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