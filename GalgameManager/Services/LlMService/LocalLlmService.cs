using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GalgameManager.Models.LLM;
using GalgameManager.WinApp.Base.Contracts;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;

public class LocalLlmService : ILlMService
{
    private readonly HttpClient _httpClient;

    public LocalLlmService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<ChatMessage> ChatAsync(string prompt)
    {
        // This method should not be used directly
        // Configuration should be passed through the ChatAsync(messages, config) method
        throw new InvalidOperationException("LocalLlmService requires configuration. Use ChatAsync(messages, config) instead.");
    }

    public async Task<ChatMessage> ChatAsync(List<ChatMessage> messages, LlMProvider config)
    {
        var (endpoint, model) = ParseConfig(config);

        // If no model specified, try to use a default
        if (string.IsNullOrEmpty(model))
        {
            // Try to get available models from the endpoint
            model = await GetDefaultModel(endpoint) ?? "llama2";
        }

        // Try to detect the local LLM service type and format request accordingly
        if (endpoint.Contains("11434"))
        {
            // Ollama API format
            return await ChatWithOllama(messages, endpoint, model);
        }
        else if (endpoint.Contains("api"))
        {
            // Generic OpenAI-compatible API format
            return await ChatWithOpenAiCompatible(messages, endpoint, model);
        }
        else
        {
            // Try to execute as command line (for llama.cpp etc.)
            return await ChatWithCommandLine(messages, config);
        }
    }

    private (string endpoint, string model) ParseConfig(LlMProvider provider)
    {
        // For local LLM, BaseUrl should contain the endpoint (e.g., http://localhost:11434)
        var endpoint = provider.BaseUrl;
        var model = provider.Model;

        // Set default endpoint if not provided
        if (string.IsNullOrEmpty(endpoint))
        {
            endpoint = "http://localhost:11434"; // Default to Ollama endpoint
        }

        if (!endpoint.EndsWith("/")) endpoint += "/";
        return (endpoint, model);
    }

    private async Task<ChatMessage> ChatWithOllama(List<ChatMessage> messages, string endpoint, string model)
    {
        var requestBody = new
        {
            model,
            messages = messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToList(),
            stream = false
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync($"{endpoint}api/chat", content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseString);
        var messageContent = json["message"]?["content"]?.ToString() ?? string.Empty;

        return new ChatMessage(ChatRole.Assistant, messageContent);
    }

    private async Task<ChatMessage> ChatWithOpenAiCompatible(List<ChatMessage> messages, string endpoint, string model)
    {
        var requestBody = new
        {
            model,
            messages = messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToList()
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync($"{endpoint}v1/chat/completions", content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(responseString);
        var messageContent = json["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;

        return new ChatMessage(ChatRole.Assistant, messageContent);
    }

    private async Task<ChatMessage> ChatWithCommandLine(List<ChatMessage> messages, LlMProvider config)
    {
        // For command-line execution, we'll use the BaseUrl as the executable path
        // and ApiKey as additional parameters if needed
        var exePath = config.BaseUrl;

        if (string.IsNullOrEmpty(exePath))
        {
            throw new InvalidOperationException("No executable path specified for local LLM");
        }

        // Create prompt from messages
        var prompt = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--prompt \"{prompt}\" --model {config.Model}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process: {exePath}");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}: {error}");
        }

        return new ChatMessage(ChatRole.Assistant, output.Trim());
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