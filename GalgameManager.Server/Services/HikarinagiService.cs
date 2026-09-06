using System.Net.Http.Headers;
using System.Text;
using System.Web;
using GalgameManager.Core.Helpers;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Models;

namespace GalgameManager.Server.Services;

public class HikarinagiService : IHikarinagiService
{
    private const string TokenEndpoint = "https://id.hikarinagi.org/oidc/token";
    private const string AuthorizationEndpoint = "https://id.hikarinagi.org/oidc/auth";
    private const string ApiBase = "https://www.hikarinagi.org/api/v3/open/";
    private const string CatalogScope = "catalog:read";
    private const string UserScope = "status:write offline_access";

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private long _tokenExpireAt; // unix秒

    public bool IsEnable { get; }
    public bool IsOAuth2Enable { get; }

    public HikarinagiService(IConfiguration config) : this(config, Utils.GetDefaultHttpClient()) { }

    // 供测试注入自定义HttpClient
    public HikarinagiService(IConfiguration config, HttpClient httpClient)
    {
        _clientId = config["AppSettings:Hikarinagi:ClientId"] ?? string.Empty;
        _clientSecret = config["AppSettings:Hikarinagi:ClientSecret"] ?? string.Empty;
        _redirectUri = config["AppSettings:Hikarinagi:RedirectUri"] ?? string.Empty;
        IsEnable = Convert.ToBoolean(config["AppSettings:Hikarinagi:Enable"] ?? "False");
        IsOAuth2Enable = Convert.ToBoolean(config["AppSettings:Hikarinagi:OAuth2Enable"] ?? "False");
        _httpClient = httpClient;
    }

    public string GetAuthorizationUrl(string state, string codeChallenge)
    {
        EnsureOAuthEnabled();
        if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("State is required.");
        if (string.IsNullOrWhiteSpace(codeChallenge)) throw new ArgumentException("Code challenge is required.");

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = _clientId;
        query["redirect_uri"] = _redirectUri;
        query["scope"] = UserScope;
        query["prompt"] = "consent";
        query["state"] = state;
        query["code_challenge"] = codeChallenge;
        query["code_challenge_method"] = "S256";
        return $"{AuthorizationEndpoint}?{query}";
    }

    public async Task<HikarinagiToken> GetUserTokenWithCodeAsync(string code, string codeVerifier)
    {
        EnsureOAuthEnabled();
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required.");
        if (string.IsNullOrWhiteSpace(codeVerifier)) throw new ArgumentException("Code verifier is required.");
        return await RequestUserTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _redirectUri,
            ["code_verifier"] = codeVerifier,
        });
    }

    public async Task<HikarinagiToken> GetUserTokenWithRefreshTokenAsync(string refreshToken)
    {
        EnsureOAuthEnabled();
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new ArgumentException("Refresh token is required.");
        HikarinagiToken token = await RequestUserTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        });
        if (string.IsNullOrEmpty(token.RefreshToken)) token.RefreshToken = refreshToken;
        return token;
    }

    public async Task<ScraperProxyResult> ProxyAsync(string path, string query)
    {
        if (IsEnable == false)
            throw new InvalidOperationException("Hikarinagi is not enabled.");
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.");
        path = path.TrimStart('/');
        if (path.Contains(".."))
            throw new ArgumentException("Invalid path.");

        HttpResponseMessage response = await SendWithTokenAsync(path, query, await GetTokenAsync(false));
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // 缓存的token可能已失效，强制刷新后重试一次
            response = await SendWithTokenAsync(path, query, await GetTokenAsync(true));
        }
        string body = await response.Content.ReadAsStringAsync();
        string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ScraperProxyResult((int)response.StatusCode, body, contentType);
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(string path, string query, string token)
    {
        HttpRequestMessage request = new(HttpMethod.Get, ApiBase + path + query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 获取client_credentials访问令牌，带缓存（提前5分钟过期）
    /// </summary>
    /// <param name="forceRefresh">是否无视缓存强制刷新</param>
    private async Task<string> GetTokenAsync(bool forceRefresh)
    {
        await _tokenLock.WaitAsync();
        try
        {
            if (forceRefresh == false && _cachedToken is not null && DateTime.Now.ToUnixTime() < _tokenExpireAt)
                return _cachedToken;
            HttpRequestMessage request = new(HttpMethod.Post, TokenEndpoint);
            string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "scope", CatalogScope },
            });
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            JsonNode json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            _cachedToken = json["access_token"]!.ToString();
            long expiresIn = json["expires_in"] is null ? 3600 : Convert.ToInt64(json["expires_in"]!.ToString());
            _tokenExpireAt = DateTime.Now.AddSeconds(expiresIn - 300).ToUnixTime();
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HikarinagiToken> RequestUserTokenAsync(Dictionary<string, string> parameters)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, TokenEndpoint);
        string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(parameters);
        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode == false)
            throw new HttpRequestException(body, null, response.StatusCode);

        JsonNode json = JsonNode.Parse(body)!;
        long expiresIn = json["expires_in"]?.GetValue<long>() ?? 3600;
        return new HikarinagiToken
        {
            AccessToken = json["access_token"]!.ToString(),
            RefreshToken = json["refresh_token"]?.ToString() ?? string.Empty,
            TokenType = json["token_type"]?.ToString() ?? "Bearer",
            Scope = json["scope"]?.ToString() ?? UserScope,
            Expires = DateTime.Now.AddSeconds(expiresIn).ToUnixTime(),
        };
    }

    private void EnsureOAuthEnabled()
    {
        if (IsOAuth2Enable == false)
            throw new InvalidOperationException("Hikarinagi OAuth is not enabled.");
    }
}
