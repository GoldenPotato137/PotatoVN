using System.Net;
using System.Text.Json.Nodes;
using GalgameManager.Core.Helpers;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Models;

namespace GalgameManager.Server.Services;

public class BangumiService(IConfiguration config) : IBangumiService
{
    private readonly string _appId = config["AppSettings:Bangumi:AppId"] ?? string.Empty;
    private readonly string _appSecret = config["AppSettings:Bangumi:AppSecret"] ?? string.Empty;
    private readonly string _redirectUri = config["AppSettings:Bangumi:RedirectUri"] ?? string.Empty;
    private readonly HttpClient _httpClient = Utils.GetDefaultHttpClient();
    public bool IsOauth2Enable { get; } = Convert.ToBoolean(config["AppSettings:Bangumi:OAuth2Enable"] ?? "False");
    public bool IsLoginEnable { get; } = Convert.ToBoolean(config["AppSettings:User:Bangumi"] ?? "False");

    public async Task<BangumiToken> GetTokenWithCodeAsync(string code)
    {
        if (IsOauth2Enable == false)
            throw new InvalidOperationException("Bangumi is not enabled.");
        Dictionary<string, string> payload = new()
        {
            { "grant_type", "authorization_code" },
            { "client_id", _appId },
            { "client_secret", _appSecret },
            { "code", code },
            { "redirect_uri", _redirectUri }
        };
        HttpResponseMessage response =
            await _httpClient.PostAsync("https://bgm.tv/oauth/access_token", payload.ToJsonContent());
        if(response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidAuthorizationCodeException(await response.Content.ReadAsStringAsync());
        response.EnsureSuccessStatusCode();
        JsonNode json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return new BangumiToken
        {
            Token = json["access_token"]!.ToString(),
            RefreshToken = json["refresh_token"]!.ToString(),
            Expires = DateTime.Now.AddSeconds(Convert.ToInt64(json["expires_in"]!.ToString())).ToUnixTime(),
            UserId = Convert.ToInt32(json["user_id"]!.ToString())
        };
    }

    public async Task<BangumiToken> GetTokenWithRefreshTokenAsync(string refreshToken)
    {
        if (IsOauth2Enable == false)
            throw new InvalidOperationException("Bangumi is not enabled.");
        Dictionary<string, string> payload = new()
        {
            { "grant_type", "refresh_token" },
            { "client_id", _appId },
            { "client_secret", _appSecret },
            { "refresh_token", refreshToken },
            { "redirect_uri", _redirectUri }
        };
        HttpResponseMessage response =
            await _httpClient.PostAsync("https://bgm.tv/oauth/access_token", payload.ToJsonContent());
        if(response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidAuthorizationCodeException(await response.Content.ReadAsStringAsync());
        response.EnsureSuccessStatusCode();
        JsonNode json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return new BangumiToken
        {
            Token = json["access_token"]!.ToString(),
            RefreshToken = json["refresh_token"]!.ToString(),
            Expires = DateTime.Now.AddSeconds(Convert.ToInt64(json["expires_in"]!.ToString())).ToUnixTime(),
            UserId = Convert.ToInt32(json["user_id"]!.ToString())
        };
    }

    public async Task<BangumiToken> GetTokenWithTokenAsync(string token)
    {
        if (IsOauth2Enable == false)
            throw new InvalidOperationException("Bangumi is not enabled.");
        Dictionary<string, string> payload = new()
        {
            { "access_token", token }
        };
        HttpResponseMessage response = await _httpClient.PostAsync("https://bgm.tv/oauth/token_status", 
            new FormUrlEncodedContent(payload));
        if(response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidAuthorizationCodeException(await response.Content.ReadAsStringAsync());
        response.EnsureSuccessStatusCode();
        JsonNode json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return new BangumiToken
        {
            Token = json["access_token"]!.ToString(),
            RefreshToken = string.Empty,
            Expires = DateTime.Now.AddSeconds(Convert.ToInt64(json["expires_in"]!.ToString())).ToUnixTime(),
            UserId = Convert.ToInt32(json["user_id"]!.ToString())
        };
    }

    public async Task<BangumiAccount> GetAccount(string token)
    {
        HttpClient tmpClient = Utils.GetDefaultHttpClient();
        tmpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        HttpResponseMessage response = await tmpClient.GetAsync("https://api.bgm.tv/v0/me");
        if(response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidAuthorizationCodeException(await response.Content.ReadAsStringAsync());
        response.EnsureSuccessStatusCode();
        JsonNode json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return new BangumiAccount
        {
            Id = Convert.ToInt32(json["id"]!.ToString()),
            UserName = (json["username"] ?? json["nickname"])!.ToString(),
            UserDisplayName = (json["nickname"] ?? json["username"])!.ToString()
        };
    }
}