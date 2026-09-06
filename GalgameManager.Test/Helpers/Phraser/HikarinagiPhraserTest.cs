using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class HikarinagiPhraserTest
{
    private StubHttpMessageHandler _handler = null!;
    private HikarinagiPhraser _phraser = null!;

    [SetUp]
    public void Setup()
    {
        _handler = new StubHttpMessageHandler();
        _phraser = new HikarinagiPhraser(new HttpClient(_handler), "https://test-server", "test_token");
    }

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task GetGalgameInfo_WithExistingId_SkipsSearchAndMapsFields()
    {
        // Arrange
        _handler.EnqueueJson("""
        {
            "success": true,
            "data": {
                "id": 780, "origin_title": "千恋＊万花", "trans_title": "千恋万花",
                "origin_intro": "日文简介", "trans_intro": "中文简介",
                "engine": "吉里吉里2", "release_date": "2016-07-29",
                "covers": [
                    { "url": "https://img/cover_low.jpg", "votes": 0 },
                    { "url": "https://img/cover_high.jpg", "votes": 10 }
                ],
                "tags": [ { "name": "恋爱" }, { "name": "巫女" } ]
            }
        }
        """);
        _handler.EnqueueJson("""
        {
            "success": true,
            "data": [
                { "role": "MAIN", "character": { "id": 7920, "name": "朝武 芳乃", "trans_name": "朝武芳乃",
                    "image": { "url": "https://img/char.jpg" } }, "actors": [] }
            ]
        }
        """);
        Galgame game = new()
        {
            Name = { Value = "千恋＊万花" },
            Ids = { [(int)RssType.Hikarinagi] = "780" },
        };

        // Act
        Galgame? result = await _phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Id, Is.EqualTo("780"));
            Assert.That(result.Name.Value, Is.EqualTo("千恋＊万花"));
            Assert.That(result.CnName, Is.EqualTo("千恋万花"));
            Assert.That(result.Description.Value, Is.EqualTo("中文简介"));
            Assert.That(result.Engine.Value, Is.EqualTo("吉里吉里2"));
            Assert.That(result.ImageUrl, Is.EqualTo("https://img/cover_high.jpg")); // 票数最高的封面
            Assert.That(result.ReleaseDate.Value, Is.EqualTo(new DateTime(2016, 7, 29)));
            Assert.That(result.Tags.Value, Is.EquivalentTo(new[] { "恋爱", "巫女" }));
            Assert.That(result.Characters, Has.Count.EqualTo(1));
            Assert.That(result.Characters[0].Name, Is.EqualTo("朝武芳乃"));
            Assert.That(result.Characters[0].Relation, Is.EqualTo("主角"));
            Assert.That(result.Characters[0].Ids[(int)RssType.Hikarinagi], Is.EqualTo("7920"));
        });
        // 已有id时不应调用搜索接口
        Assert.That(_handler.Requests.Any(r => r.Contains("/search")), Is.False);
        Assert.That(_handler.Requests[0], Does.Contain("/phraser/hikarinagi/galgames/780"));
    }

    [Test]
    public async Task GetGalgameInfo_WithoutId_SearchesAndUsesDeveloperFromSearch()
    {
        // Arrange
        _handler.EnqueueJson("""
        {
            "success": true,
            "data": {
                "items": [
                    { "type": "galgame", "id": 1, "title": "完全不相关", "developer": "A社" },
                    { "type": "galgame", "id": 780, "title": "千恋＊万花", "developer": "ゆずソフト" }
                ],
                "meta": { "page": 1, "page_size": 10, "total_items": 2, "total_pages": 1, "item_count": 2 }
            }
        }
        """);
        _handler.EnqueueJson("""
        {
            "success": true,
            "data": { "id": 780, "origin_title": "千恋＊万花", "trans_title": null,
                "origin_intro": "日文简介", "trans_intro": null, "covers": [], "tags": [] }
        }
        """);
        _handler.EnqueueJson("""{ "success": true, "data": [] }""");
        Galgame game = new() { Name = { Value = "千恋＊万花" } };

        // Act
        Galgame? result = await _phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Id, Is.EqualTo("780"));
            Assert.That(result.Developer.Value, Is.EqualTo("ゆずソフト")); // developer来自搜索结果
            Assert.That(result.Description.Value, Is.EqualTo("日文简介")); // trans_intro为空时回退origin_intro
        });
        Assert.That(_handler.Requests[0], Does.Contain("/phraser/hikarinagi/search?q="));
        Assert.That(_handler.Requests[0], Does.Contain("types=galgame"));
    }

    [Test]
    public async Task GetGalgameInfo_ReturnsNull_WhenSearchNoResult()
    {
        // Arrange
        _handler.EnqueueJson("""{ "success": true, "data": { "items": [] } }""");
        Galgame game = new() { Name = { Value = "不存在的游戏xyz" } };

        // Act
        Galgame? result = await _phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetGalgameInfo_ReturnsNull_WhenBestMatchSimilarityTooLow()
    {
        // Arrange：搜索有结果但全部与查询无关（真实案例：Hikarinagi未收录"承命抉择Oblige"，
        // 最相似的候选是id=12423"命运线"，相似度仅约0.48，不应错误命中）
        _handler.EnqueueJson("""
        {
            "success": true,
            "data": {
                "items": [
                    { "type": "galgame", "id": 16226, "title": "你与选择", "subtitle": null, "developer": "灵虚之幽" },
                    { "type": "galgame", "id": 13428, "title": "寒蝉鸣泣之时 命", "subtitle": null, "developer": "マイネットゲームス" },
                    { "type": "galgame", "id": 12423, "title": "命运线", "subtitle": null, "developer": null }
                ]
            }
        }
        """);
        // 相似度不足0.9时会拉取前3个候选的详情重新匹配，详情标题仍然不相似
        _handler.EnqueueJson("""{ "success": true, "data": { "id": 16226, "origin_title": "你与选择", "trans_title": null } }""");
        _handler.EnqueueJson("""{ "success": true, "data": { "id": 13428, "origin_title": "ひぐらしのなく頃に 命", "trans_title": "寒蝉鸣泣之时 命" } }""");
        _handler.EnqueueJson("""{ "success": true, "data": { "id": 12423, "origin_title": "命运线", "trans_title": null } }""");
        Galgame game = new() { Name = { Value = "承命抉择Oblige" } };

        // Act
        Galgame? result = await _phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Null);
        Assert.That(_handler.Requests, Has.Count.EqualTo(4)); // 搜索 + 3个候选详情，不应再请求游戏信息
    }

    [Test]
    public async Task GetGalgameInfo_ReturnsNull_WhenUpstreamFails()
    {
        // Arrange
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("server error", Encoding.UTF8, "text/plain"),
        });
        Galgame game = new()
        {
            Name = { Value = "x" },
            Ids = { [(int)RssType.Hikarinagi] = "1" },
        };

        // Act
        Galgame? result = await _phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Requests_CarryBearerToken()
    {
        // Arrange
        _handler.EnqueueJson("""{ "success": true, "data": { "items": [] } }""");
        Galgame game = new() { Name = { Value = "x" } };

        // Act
        await _phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(_handler.Authorizations, Has.Count.EqualTo(1));
        Assert.That(_handler.Authorizations[0], Is.EqualTo("Bearer test_token"));
    }

    [Test]
    public async Task Requests_AreAnonymous_WhenTokenEmpty()
    {
        // Arrange
        HikarinagiPhraser phraser = new(new HttpClient(_handler), "https://test-server", string.Empty);
        _handler.EnqueueJson("""{ "success": true, "data": { "items": [] } }""");
        Galgame game = new() { Name = { Value = "x" } };

        // Act
        await phraser.GetGalgameInfo(game);

        // Assert：未登录时不带Authorization头（服务端按IP限速360次/分钟）
        Assert.That(_handler.Authorizations, Has.Count.EqualTo(1));
        Assert.That(_handler.Authorizations[0], Is.Null);
    }

    private static HttpResponseMessage RateLimitedResponse()
    {
        HttpResponseMessage response = new(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("rate limited", Encoding.UTF8, "text/plain"),
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
        return response;
    }

    [Test]
    public async Task GetGalgameInfo_JapaneseQuery_MatchesOriginTitleFromDetail()
    {
        // Arrange：搜索结果只含译名（与查询语言不一致），需拉取详情用原标题重新匹配
        // 真实案例：搜索"ライムライト・レモネードジャム"时应命中id=1041而非8008
        _handler.EnqueueJson("""
        {
            "success": true,
            "data": {
                "items": [
                    { "type": "galgame", "id": 1041, "title": "橘光柠水随想曲", "subtitle": null, "developer": "ゆずソフト" },
                    { "type": "galgame", "id": 8008, "title": "プライムガール", "subtitle": null, "developer": "ファルスタッフ" }
                ]
            }
        }
        """);
        // 候选详情：1041的原标题与查询完全一致（完全匹配后不再拉取后续候选详情）
        _handler.EnqueueJson("""
        { "success": true, "data": { "id": 1041, "origin_title": "ライムライト・レモネードジャム", "trans_title": "橘光柠水随想曲" } }
        """);
        _handler.EnqueueJson("""
        { "success": true, "data": { "id": 1041, "origin_title": "ライムライト・レモネードジャム", "covers": [], "tags": [] } }
        """);
        _handler.EnqueueJson("""{ "success": true, "data": [] }""");
        Galgame game = new() { Name = { Value = "ライムライト・レモネードジャム" } };

        // Act
        Galgame? result = await _phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("1041"));
        Assert.That(result.Developer.Value, Is.EqualTo("ゆずソフト")); // developer仍来自搜索项
        Assert.That(_handler.Requests[1], Does.Contain("/phraser/hikarinagi/galgames/1041"));
    }

    [Test]
    public async Task GetGalgameInfo_RateLimited_WaitsAndRetries()
    {
        // Arrange：第一次请求被限流（429），等待后重试成功
        var messenger = new StrongReferenceMessenger();
        List<GalgameParsingEventArgs> events = [];
        messenger.Register<GalgameParsingEventArgs>(this, (_, m) => events.Add(m));
        HikarinagiPhraser phraser = new(new HttpClient(_handler), "https://test-server", "test_token", messenger);
        _handler.Enqueue(RateLimitedResponse());
        _handler.EnqueueJson("""
        {
            "success": true,
            "data": { "id": 780, "origin_title": "千恋＊万花", "covers": [], "tags": [] }
        }
        """);
        _handler.EnqueueJson("""{ "success": true, "data": [] }""");
        Galgame game = new()
        {
            Name = { Value = "千恋＊万花" },
            Ids = { [(int)RssType.Hikarinagi] = "780" },
        };

        // Act
        Galgame? result = await phraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(_handler.Requests, Has.Count.EqualTo(3)); // 限流重试 + 游戏信息 + 角色列表
        Assert.That(events, Is.Not.Empty); // 等待期间上报了搜刮状态
        Assert.That(events.All(e => ReferenceEquals(e.Galgame, game)), Is.True);
        messenger.UnregisterAll(this);
    }

    [Test]
    public async Task GetGalgameInfo_RateLimited_GivesUpAfterMaxRetries()
    {
        // Arrange：持续限流（初次请求 + 3次重试，共4次）
        for (var i = 0; i < 4; i++) _handler.Enqueue(RateLimitedResponse());
        Galgame game = new()
        {
            Name = { Value = "x" },
            Ids = { [(int)RssType.Hikarinagi] = "1" },
        };

        // Act
        Galgame? result = await _phraser.GetGalgameInfo(game);

        // Assert：重试3次后放弃
        Assert.That(result, Is.Null);
        Assert.That(_handler.Requests, Has.Count.EqualTo(4));
    }

    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<string> Requests { get; } = [];
        public List<string?> Authorizations { get; } = [];

        public void EnqueueJson(string body) => Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            Authorizations.Add(request.Headers.Authorization?.ToString());
            if (_responses.Count == 0)
                throw new InvalidOperationException("No stubbed response left.");
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
