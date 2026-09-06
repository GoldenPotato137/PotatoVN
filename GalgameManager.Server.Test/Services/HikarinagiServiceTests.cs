using System.Net;
using System.Text;
using GalgameManager.Server.Services;
using Microsoft.Extensions.Configuration;

namespace GalgameManager.Server.Test.Services;

/// <summary>
/// Hikarinagi透传代理服务测试
/// </summary>
[TestFixture]
public class HikarinagiServiceTests
{
    private static IConfiguration CreateConfig(bool enable = true)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppSettings:Hikarinagi:Enable"] = enable.ToString(),
            ["AppSettings:Hikarinagi:ClientId"] = "test_id",
            ["AppSettings:Hikarinagi:ClientSecret"] = "test_secret",
            ["AppSettings:Hikarinagi:OAuth2Enable"] = enable.ToString(),
            ["AppSettings:Hikarinagi:RedirectUri"] = "potato-vn://oauth-hikarinagi",
        }).Build();
    }

    [Test]
    public void GetAuthorizationUrl_IncludesPkceStateAndUserScopes()
    {
        // Arrange
        HikarinagiService service = new(CreateConfig(), new HttpClient(new StubHttpMessageHandler()));

        // Act
        string url = service.GetAuthorizationUrl("state_value", "challenge_value");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.StartWith("https://id.hikarinagi.org/oidc/auth?"));
            Assert.That(url, Does.Contain("client_id=test_id"));
            Assert.That(url, Does.Contain("redirect_uri=potato-vn%3a%2f%2foauth-hikarinagi"));
            Assert.That(url, Does.Contain("scope=status%3awrite+offline_access"));
            Assert.That(url, Does.Contain("state=state_value"));
            Assert.That(url, Does.Contain("code_challenge=challenge_value"));
            Assert.That(url, Does.Contain("code_challenge_method=S256"));
        });
    }

    [Test]
    public async Task GetUserTokenWithCodeAsync_UsesConfidentialClientAndPkce()
    {
        // Arrange
        StubHttpMessageHandler handler = new();
        handler.EnqueueJson(HttpStatusCode.OK,
            """{"access_token":"access_1","refresh_token":"refresh_1","expires_in":3600,"token_type":"Bearer","scope":"status:write"}""");
        HikarinagiService service = new(CreateConfig(), new HttpClient(handler));

        // Act
        var token = await service.GetUserTokenWithCodeAsync("code_value", "verifier_value");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(token.AccessToken, Is.EqualTo("access_1"));
            Assert.That(token.RefreshToken, Is.EqualTo("refresh_1"));
            Assert.That(token.Scope, Is.EqualTo("status:write"));
            Assert.That(token.Expires, Is.GreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            Assert.That(handler.Requests[0].Authorization, Is.EqualTo(
                "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test_id:test_secret"))));
            Assert.That(handler.Requests[0].Body, Does.Contain("grant_type=authorization_code"));
            Assert.That(handler.Requests[0].Body, Does.Contain("code=code_value"));
            Assert.That(handler.Requests[0].Body, Does.Contain("code_verifier=verifier_value"));
            Assert.That(handler.Requests[0].Body,
                Does.Contain("redirect_uri=potato-vn%3A%2F%2Foauth-hikarinagi"));
        });
    }

    [Test]
    public async Task GetUserTokenWithRefreshTokenAsync_ReturnsRotatedRefreshToken()
    {
        // Arrange
        StubHttpMessageHandler handler = new();
        handler.EnqueueJson(HttpStatusCode.OK,
            """{"access_token":"access_2","refresh_token":"refresh_2","expires_in":3600,"token_type":"Bearer","scope":"status:write"}""");
        HikarinagiService service = new(CreateConfig(), new HttpClient(handler));

        // Act
        var token = await service.GetUserTokenWithRefreshTokenAsync("refresh_1");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(token.AccessToken, Is.EqualTo("access_2"));
            Assert.That(token.RefreshToken, Is.EqualTo("refresh_2"));
            Assert.That(handler.Requests[0].Body, Does.Contain("grant_type=refresh_token"));
            Assert.That(handler.Requests[0].Body, Does.Contain("refresh_token=refresh_1"));
        });
    }

    [Test]
    public async Task ProxyAsync_FetchesTokenAndProxiesRequest()
    {
        // Arrange
        StubHttpMessageHandler handler = new();
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"token_1","expires_in":3600}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"success":true,"data":{"id":1}}""");
        HikarinagiService service = new(CreateConfig(), new HttpClient(handler));

        // Act
        var result = await service.ProxyAsync("galgames/1", "?page=1");

        // Assert
        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(result.Body, Does.Contain("\"success\":true"));
        Assert.That(result.ContentType, Does.Contain("application/json"));
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
        // 第一个请求：获取token（Basic认证 + client_credentials）
        Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(handler.Requests[0].Url, Is.EqualTo("https://id.hikarinagi.org/oidc/token"));
        Assert.That(handler.Requests[0].Authorization, Is.EqualTo(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test_id:test_secret"))));
        Assert.That(handler.Requests[0].Body, Does.Contain("grant_type=client_credentials"));
        // 第二个请求：带Bearer token透传
        Assert.That(handler.Requests[1].Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(handler.Requests[1].Url,
            Is.EqualTo("https://www.hikarinagi.org/api/v3/open/galgames/1?page=1"));
        Assert.That(handler.Requests[1].Authorization, Is.EqualTo("Bearer token_1"));
    }

    [Test]
    public async Task ProxyAsync_CachesTokenAcrossRequests()
    {
        // Arrange
        StubHttpMessageHandler handler = new();
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"token_1","expires_in":3600}""");
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        HikarinagiService service = new(CreateConfig(), new HttpClient(handler));

        // Act
        await service.ProxyAsync("galgames/1", string.Empty);
        await service.ProxyAsync("galgames/2", string.Empty);

        // Assert：token只获取一次，之后复用缓存
        Assert.That(handler.Requests.Count(r => r.Method == HttpMethod.Post), Is.EqualTo(1));
        Assert.That(handler.Requests[2].Authorization, Is.EqualTo("Bearer token_1"));
    }

    [Test]
    public async Task ProxyAsync_RefreshesTokenAndRetries_WhenUpstreamReturns401()
    {
        // Arrange
        StubHttpMessageHandler handler = new();
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"token_1","expires_in":3600}""");
        handler.EnqueueJson(HttpStatusCode.Unauthorized, """{"success":false}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"token_2","expires_in":3600}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"success":true}""");
        HikarinagiService service = new(CreateConfig(), new HttpClient(handler));

        // Act
        var result = await service.ProxyAsync("galgames/1", string.Empty);

        // Assert：强制刷新token并重试成功
        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(handler.Requests, Has.Count.EqualTo(4));
        Assert.That(handler.Requests[3].Authorization, Is.EqualTo("Bearer token_2"));
    }

    [Test]
    public void ProxyAsync_Throws_WhenDisabled()
    {
        // Arrange
        HikarinagiService service = new(CreateConfig(false), new HttpClient(new StubHttpMessageHandler()));

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => service.ProxyAsync("galgames/1", string.Empty));
    }

    [Test]
    public void ProxyAsync_Throws_WhenPathInvalid()
    {
        // Arrange
        StubHttpMessageHandler handler = new();
        handler.EnqueueJson(HttpStatusCode.OK, """{"access_token":"token_1","expires_in":3600}""");
        HikarinagiService service = new(CreateConfig(), new HttpClient(handler));

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.ProxyAsync("", string.Empty));
        Assert.ThrowsAsync<ArgumentException>(() => service.ProxyAsync("../user/me", string.Empty));
    }

    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<(HttpMethod Method, string Url, string? Authorization, string? Body)> Requests { get; } = [];

        public void EnqueueJson(HttpStatusCode statusCode, string body)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            Requests.Add((request.Method, request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(), body));
            if (_responses.Count == 0)
                throw new InvalidOperationException("No stubbed response left.");
            return _responses.Dequeue();
        }
    }
}
