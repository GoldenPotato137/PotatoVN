using System.Net.Http.Headers;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        await Launcher.LaunchUriAsync(new Uri(BgmOAuthConfig.OAuthUrl));
    }

    public async Task FinishOAuthWithUri(string uri)
    {
        if (uri.StartsWith(BgmOAuthConfig.RedirectUri))
        {
            await FinishOAuthWithCode(uri.Split("=")[1]);
        }
        await Task.CompletedTask;
    }

    private async Task FinishOAuthWithCode(string code)
    {
        var httpClient = GetHttpClient();
        var parameters = new Dictionary<string, string>();
        parameters.Add("grant_type", "authorization_code");
        parameters.Add("client_id", BgmOAuthConfig.AppId);
        parameters.Add("client_secret", BgmOAuthConfig.AppSecret);
        parameters.Add("redirect_uri", BgmOAuthConfig.RedirectUri);
        parameters.Add("code", code);
        var requestContent = new FormUrlEncodedContent(parameters);
        var responseMessage = httpClient.PostAsync("https://bgm.tv/oauth/access_token", requestContent).Result;
        if (!responseMessage.IsSuccessStatusCode) return;
        JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiAccessToken, json["access_token"]!.ToString());
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiRefreshToken, json["refresh_token"]!.ToString());
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