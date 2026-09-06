using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using System.Web;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers.Phrase;

/// <summary>
/// Hikarinagi搜刮器，通过PotatoVN同步服务器代理访问Hikarinagi开放API
/// </summary>
public class HikarinagiPhraser : IGalInfoPhraser, IGalCharacterPhraser
{
    private readonly HttpClient? _httpClient;
    private readonly string? _baseUrl;
    private readonly string? _token;
    private readonly IMessenger? _bus;

    /// 触发限流（429）时的最大重试次数
    private const int MaxRateLimitRetries = 3;
    /// 响应未携带Retry-After时的默认等待秒数（服务器限流窗口为1分钟）
    private const int DefaultRateLimitWaitSeconds = 60;
    /// 搜索结果可接受的最低相似度；低于该值认为Hikarinagi未收录该游戏，返回null交由其他信息源处理。
    /// （实测无关条目因个别相同汉字也能达到约0.48的相似度，而正常模糊匹配一般在0.8以上）
    private const double MinSimilarity = 0.7;

    public HikarinagiPhraser(IMessenger? bus = null)
    {
        _bus = bus;
    }

    // 仅测试使用：直接指定HttpClient、服务器地址与令牌
    public HikarinagiPhraser(HttpClient httpClient, string baseUrl, string token, IMessenger? bus = null)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
        _token = token;
        _bus = bus;
    }

    public RssType GetPhraseType() => RssType.Hikarinagi;

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        (HttpClient client, string baseUrl) = await GetClientAsync();

        string? developer = null;
        int? id = null;
        try
        {
            id = Convert.ToInt32(galgame.Ids[(int)RssType.Hikarinagi] ?? string.Empty);
            if (id == 0) id = null;
        }
        catch (Exception)
        {
            // id无效，走搜索
        }
        if (id is null)
        {
            (int id, string? developer)? searchResult =
                await SearchAsync(client, baseUrl, galgame.Name.Value ?? string.Empty, galgame);
            if (searchResult is null) return null;
            id = searchResult.Value.id;
            developer = searchResult.Value.developer;
        }

        JToken data;
        try
        {
            JObject json = JObject.Parse(await GetStringWithRateLimitAsync(client,
                $"{baseUrl}/phraser/hikarinagi/galgames/{id}", galgame));
            if (json["success"]?.ToObject<bool>() != true) return null;
            data = json["data"]!;
        }
        catch (Exception)
        {
            return null;
        }

        Galgame result = new()
        {
            RssType = RssType.Hikarinagi,
            Id = data["id"]!.ToString(),
            Name = data["origin_title"]?.ToString() ?? string.Empty,
            CnName = data["trans_title"]?.ToString() ?? string.Empty,
            Description = FirstNonEmpty(data["trans_intro"]?.ToString(), data["origin_intro"]?.ToString()),
            Developer = string.IsNullOrEmpty(developer) ? Galgame.DefaultString : developer,
            Engine = string.IsNullOrEmpty(data["engine"]?.ToString())
                ? Galgame.DefaultString
                : data["engine"]!.ToString(),
            ReleaseDate = IGalInfoPhraser.GetDateTimeFromString(data["release_date"]?.ToString()) ??
                          DateTime.MinValue,
            ImageUrl = (data["covers"] as JArray)
                ?.OrderByDescending(c => c["votes"]?.ToObject<int>() ?? 0)
                .FirstOrDefault()?["url"]?.ToString(),
            Tags = new ObservableCollection<string>((data["tags"] as JArray)
                ?.Select(t => t["name"]?.ToString() ?? string.Empty)
                .Where(s => string.IsNullOrEmpty(s) == false) ?? []),
        };

        try
        {
            JObject json = await GetJsonAsync(client, $"{baseUrl}/phraser/hikarinagi/galgames/{id}/characters",
                galgame);
            if (json["success"]?.ToObject<bool>() == true && json["data"] is JArray characters)
                foreach (JToken item in characters)
                {
                    JToken? c = item["character"];
                    if (c is null) continue;
                    GalgameCharacter character = new()
                    {
                        Name = FirstNonEmpty(c["trans_name"]?.ToString(), c["name"]?.ToString()),
                        Relation = item["role"]?.ToString() switch
                        {
                            "MAIN" => "主角",
                            "SUPPORTING" => "配角",
                            "GUEST" => "客串",
                            _ => string.Empty,
                        },
                        PreviewImageUrl = c["image"]?["url"]?.ToString(),
                    };
                    character.Ids[(int)RssType.Hikarinagi] = c["id"]?.ToString();
                    result.Characters.Add(character);
                }
        }
        catch (Exception)
        {
            // 忽略角色获取失败
        }

        return result;
    }

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        (HttpClient client, string baseUrl) = await GetClientAsync();
        var id = galgameCharacter.Ids[(int)RssType.Hikarinagi];
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            JObject json = await GetJsonAsync(client, $"{baseUrl}/phraser/hikarinagi/characters/{id}");
            if (json["success"]?.ToObject<bool>() != true) return null;
            JToken data = json["data"]!;
            GalgameCharacter result = new()
            {
                Name = FirstNonEmpty(data["trans_name"]?.ToString(), data["name"]?.ToString()),
                Summary = FirstNonEmpty(data["trans_intro"]?.ToString(), data["intro"]?.ToString()),
                Gender = ParseGender(data["gender"]?.ToString()),
                BirthMon = data["birthday_month"]?.ToObject<int?>(),
                BirthDay = data["birthday_day"]?.ToObject<int?>(),
                BloodType = data["blood_type"]?.ToString(),
                Height = data["height"]?.ToString(),
                Weight = data["weight"]?.ToString(),
                ImageUrl = data["image"]?["url"]?.ToString(),
                PreviewImageUrl = data["image"]?["url"]?.ToString(),
            };
            if (data["bust"] is not null && data["waist"] is not null && data["hips"] is not null)
                result.BWH = $"{data["bust"]}/{data["waist"]}/{data["hips"]}";
            result.Ids[(int)RssType.Hikarinagi] = id;
            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 搜索游戏，返回相似度最高的条目id与开发厂商；最佳相似度低于 <see cref="MinSimilarity"/> 时视为未收录，返回null
    /// </summary>
    private async Task<(int id, string? developer)?> SearchAsync(HttpClient client, string baseUrl,
        string name, Galgame? galgame = null)
    {
        try
        {
            var url = $"{baseUrl}/phraser/hikarinagi/search?q={HttpUtility.UrlEncode(name)}" +
                      "&types=galgame&page=1&page_size=10";
            JObject json = JObject.Parse(await GetStringWithRateLimitAsync(client, url, galgame));
            if (json["success"]?.ToObject<bool>() != true) return null;
            if (json["data"]?["items"] is not JArray items || items.Count == 0) return null;

            double maxSimilarity = 0;
            JToken? target = null;
            foreach (JToken item in items)
            {
                double similarity = Math.Max(
                    TitleSimilarity(name, item["title"]?.ToString()),
                    TitleSimilarity(name, item["subtitle"]?.ToString()));
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    target = item;
                }
            }

            // 搜索结果仅含单一显示标题（可能是译名），查询语言与显示标题不一致时相似度会失真；
            // 最佳匹配度不足时拉取前几个候选的详情，用原标题与译名重新匹配
            // （例：以日文名"ライムライト・レモネードジャム"搜索时，正确条目的显示标题是译名"橘光柠水随想曲"）
            if (maxSimilarity < 0.9)
                foreach (JToken item in items.Take(3))
                {
                    try
                    {
                        JObject detail = JObject.Parse(await GetStringWithRateLimitAsync(client,
                            $"{baseUrl}/phraser/hikarinagi/galgames/{item["id"]}", galgame));
                        if (detail["success"]?.ToObject<bool>() != true) continue;
                        double similarity = Math.Max(
                            TitleSimilarity(name, detail["data"]?["origin_title"]?.ToString()),
                            TitleSimilarity(name, detail["data"]?["trans_title"]?.ToString()));
                        if (similarity > maxSimilarity)
                        {
                            maxSimilarity = similarity;
                            target = item;
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单个候选详情获取失败
                    }
                    if (maxSimilarity >= 1.0) break; // 已完全匹配，无法更优
                }

            // 相似度过低说明Hikarinagi上大概率没有该游戏，不应错误命中无关条目
            // （例：搜索"承命抉择Oblige"时最相似的是无关条目"命运线"，相似度仅约0.48）
            if (target is null || maxSimilarity < MinSimilarity) return null;
            return (target["id"]!.ToObject<int>(), target["developer"]?.ToString());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static double TitleSimilarity(string name, string? title)
        => string.IsNullOrEmpty(title) ? 0 : IGalInfoPhraser.Similarity(name, title);

    /// <summary>
    /// 获取HttpClient与服务器地址；未登录PotatoVN账户时匿名访问（服务器侧限速360次/分钟）
    /// </summary>
    private async Task<(HttpClient client, string baseUrl)> GetClientAsync()
    {
        if (_httpClient is not null)
        {
            if (!string.IsNullOrEmpty(_token))
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return (_httpClient, _baseUrl!);
        }
        HttpClient client = Utils.GetDefaultHttpClient().WithApplicationJson();
        PvnAccount? account = await App.GetService<ILocalSettingsService>()
            .ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        if (account is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
        return (client, App.GetService<IPvnService>().BaseUri.ToString().TrimEnd('/'));
    }

    private async Task<JObject> GetJsonAsync(HttpClient client, string url, Galgame? galgame = null)
        => JObject.Parse(await GetStringWithRateLimitAsync(client, url, galgame));

    /// <summary>
    /// 发起GET请求并读取响应文本；触发限流（HTTP 429）时按Retry-After等待后重试，
    /// 等待期间通过 <see cref="GalgameParsingEventArgs"/> 反馈搜刮状态（倒计时）
    /// </summary>
    private async Task<string> GetStringWithRateLimitAsync(HttpClient client, string url, Galgame? galgame = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= MaxRateLimitRetries)
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            TimeSpan wait = response.Headers.RetryAfter?.Delta
                            ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.Now)
                            ?? TimeSpan.FromSeconds(DefaultRateLimitWaitSeconds);
            var waitSeconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
            for (var remaining = waitSeconds; remaining > 0; remaining--)
            {
                if (galgame is not null)
                    _bus?.Send(new GalgameParsingEventArgs(galgame,
                        "HikarinagiPhraser_RateLimitWaiting".GetLocalized(remaining, attempt + 1,
                            MaxRateLimitRetries)));
                await Task.Delay(1000);
            }
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(s => string.IsNullOrEmpty(s) == false) ?? string.Empty;

    private static Gender ParseGender(string? gender)
    {
        var lower = gender?.ToLowerInvariant();
        if (lower is "female" or "f" or "女") return Gender.Female;
        if (lower is "male" or "m" or "男") return Gender.Male;
        return Gender.Unknown;
    }
}
