using System.Net.Http.Headers;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;

public class BgmOAuthService : IBgmOAuthService
{
    private readonly ILocalSettingsService _localSettingsService;

    public BgmOAuthService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task StartOAuth()
    {
        await Task.CompletedTask;
    }

    public async Task FinishOAuthWithUri(string uri)
    {
        var parts = uri.Split("://")[1].Split("/");
        if (parts[0] == "bgm_oauth")
        {
            await FinishOAuthWithCode(parts[1]);
        }
        await Task.CompletedTask;
    }

    private async Task FinishOAuthWithCode(string code)
    {
        var httpClient = GetHttpClient();
        var parameters = new Dictionary<string, string>();
        parameters.Add("grant_type", "authorization_code");
        parameters.Add("client_id", "bgm273264c1e79e6c30c");
        parameters.Add("client_secret", "6aaad2643c4abfc6393860262b092338");
        parameters.Add("code", code);
        var requestContent = new FormUrlEncodedContent(parameters);
        var responseMessage = httpClient.PostAsync("https://bgm.tv/oauth/access_token", requestContent).Result;
        if (!responseMessage.IsSuccessStatusCode) return;
        JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiToken, json["access_token"]!.ToString());
        await Task.CompletedTask;
    }

    
    private HttpClient GetHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "GoldenPotato/GalgameManager/1.0-dev (Windows) (https://github.com/GoldenPotato137/GalgameManager)");
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}