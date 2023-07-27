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
        parameters.Add("client_id", BgmOAuthConfig.AppId);
        parameters.Add("client_secret", BgmOAuthConfig.AppSecret);
        parameters.Add("redirect_uri", BgmOAuthConfig.RedirectUri);
        parameters.Add("code", code);
        var requestContent = new FormUrlEncodedContent(parameters);
        var responseMessage = httpClient.PostAsync("https://bgm.tv/oauth/access_token", requestContent).Result;
        if (!responseMessage.IsSuccessStatusCode) return;
        JObject json = JObject.Parse(responseMessage.Content.ReadAsStringAsync().Result);
        await _localSettingsService.SaveSettingAsync(KeyValues.BangumiToken, json["access_token"]!.ToString());
        
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "Bgm",
            PrimaryButtonText = "App_UnhandledException_BackToHome".GetLocalized(),
            CloseButtonText = "App_UnhandledException_Exit".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary
        };
        StackPanel stackPanel = new();
        stackPanel.Children.Add(new TextBlock()
        {
            Text = responseMessage.Content.ReadAsStringAsync().Result,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        dialog.Content = stackPanel;
        await dialog.ShowAsync();
        
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