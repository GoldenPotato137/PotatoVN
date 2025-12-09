using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
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

    public async Task<ChatMessage> ChatAsync(string prompt)
    {
        // This method should not be used directly
        // Configuration should be passed through the ChatAsync(messages, config) method
        throw new InvalidOperationException("OpenAiService requires configuration. Use ChatAsync(messages, config) instead.");
    }

    public async Task<ChatMessage> ChatAsync(List<ChatMessage> messages, LlMProvider provider)
    {
        var (apiKey, baseUrl, model) = ParseConfig(provider);

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

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseString);
        var messageContent = json["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;

        return new ChatMessage(ChatRole.Assistant, messageContent);
    }

    private (string apiKey, string baseUrl, string model) ParseConfig(LlMProvider provider)
    {
        var apiKey = provider.ApiKey;
        var baseUrl = provider.BaseUrl;
        var model = provider.Model;

        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return (apiKey, baseUrl, model);
    }
}