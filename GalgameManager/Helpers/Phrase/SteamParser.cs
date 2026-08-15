using System.Collections.ObjectModel;
using System.Web;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers.API.Steam;
using GalgameManager.Models;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers.Phrase;

public class SteamParser : IGalInfoPhraser, IGalHeaderParser, IGalCoversParser, IGalHeadersParser
{
    private readonly string _lang;
    private readonly HttpClient _httpClient;
    private readonly ISteamStoreApi _storeApi;

    public SteamParser(string lang)
    {
        _lang = lang;
        _httpClient = Utils.GetDefaultHttpClient();
        _httpClient.BaseAddress = new Uri("https://api.steampowered.com");
        _storeApi = SteamAPi.GetStoreApi();
    }

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        var appId = TryParseId(galgame);
        if (appId is null && galgame.Name.Value is not null)
            appId = await QueryAppIdByNameAsync(galgame);
        if (appId is null) return null;

        Dictionary<string, SteamAppDetailResponse>? dict;
        try
        {
            dict = await _storeApi.GetAppDetailsAsync(appId.ToString()!, _lang);
        }
        catch
        {
            return null;
        }

        if (dict.TryGetValue(appId.ToString()!, out SteamAppDetailResponse? rsp) == false || rsp.Success == false ||
            rsp.Data is null)
            return null;

        SteamAppDetailDataDto data = rsp.Data;
        Galgame result = new()
        {
            RssType = RssType.Steam,
            Name = data.Name ?? string.Empty,
            Description = data.DetailedDescription ?? string.Empty,
            ImageUrl = data.HeaderImage,
            Developer = data.Developers is { Count: > 0 } ? data.Developers[0] : Galgame.DefaultString,
            ReleaseDate = DateTimeExtensions.ToDateTime(data.ReleaseDate?.Date ?? string.Empty),
            Tags = new ObservableCollection<string>(data.Genres?.Select(g => g.Description ?? string.Empty) ?? []),
            Ids =
            {
                [(int)GetPhraseType()] = appId.ToString(),
            },
        };
        result.ChineseName = result.Name;
        result.OriginalName = result.Name;
        // 清理描述里面的HTML标签
        result.Description = Utils.CleanHtmlTags(result.Description.Value ?? string.Empty);
        result.LastFetchInfoTime = DateTime.Now;
        result.ImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
        return result;
    }

    public RssType GetPhraseType() => RssType.Steam;

    public async Task<string?> GetGalHeaderAsync(Galgame galgame)
    {
        var appId = TryParseId(galgame);
        if (appId is null && galgame.Name.Value is not null)
            appId = await QueryAppIdByNameAsync(galgame);
        if (appId is null) return null;
        return $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_hero.jpg";
    }

    /// <summary>
    /// 获取Headers图片
    /// </summary>
    public async Task<List<string>> GetGalHeadersAsync(Galgame game) =>
        (await GetGalHeaderAsync(game)) is { } header ? [header] : [];

    /// <summary>
    /// 获取封面图片，只关注ImageUrl
    /// </summary>
    public async Task<List<string>> GetGalCoversAsync(Galgame galgame)
    {
        var appId = TryParseId(galgame);
        if (appId is null && galgame.Name.Value is not null)
            appId = await QueryAppIdByNameAsync(galgame);
        if (appId is null) return [];

        Dictionary<string, SteamAppDetailResponse>? dict;
        try
        {
            dict = await _storeApi.GetAppDetailsAsync(appId.ToString()!, _lang);
        }
        catch
        {
            return [];
        }

        if (dict.TryGetValue(appId.ToString()!, out SteamAppDetailResponse? rsp) == false || rsp.Success == false ||
            rsp.Data is null)
            return [];
        
        // If we reached here, the AppId is considered valid, so construct the cover URL
        return [$"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_600x900.jpg"];
    }

    #region private helpers

    private static int? TryParseId(Galgame gal)
    {
        try { return Convert.ToInt32(gal.Ids[(int)RssType.Steam] ?? string.Empty); }
        catch { return null; }
    }

    /// <summary>
    /// 通过模糊搜索拿到 Steam AppID
    /// </summary>
    private async Task<int?> QueryAppIdByNameAsync(Galgame game)
    {
        try
        {
            if (await PhraseHelper.TryGetMapAsync(game) is { SteamId: > 0 } map &&
                map.SteamSimilarity >= PhraseHelper.MinSimilarity) return map.SteamId;
            if (string.IsNullOrEmpty(game.Name.Value)) return null;
            List<string> nameLists = await PhraseHelper.TryGetAliasesAsync(game.Name.Value);
            if (!nameLists.Contains(game.Name.Value)) nameLists.Insert(0, game.Name.Value);
            Dictionary<string, JArray?> cache = new();
            double max = 0;
            int? id = null;
            foreach (var name in nameLists)
            {
                var langStr = LanguageEnum.English.ToSteamApiString();
                if (name.IsJapanese()) langStr = LanguageEnum.Japanese.ToSteamApiString();
                else if (name.IsChinese()) langStr = LanguageEnum.ChineseSimplified.ToSteamApiString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!cache.TryGetValue(langStr, out JArray? items))
                {
                    var url =
                        $"https://store.steampowered.com/api/storesearch?term={HttpUtility.UrlEncode(name)}&l={langStr}&cc=US";
                    var json = await _httpClient.GetStringAsync(url);
                    JObject jo = JObject.Parse(json);
                    items = jo["items"] as JArray ?? jo["apps"] as JArray;
                    cache[langStr] = items;
                }
                if (items is null || items.Count == 0) continue;
                foreach (JToken it in items)
                {
                    var itemName = it["name"]?.ToObject<string>();
                    if (itemName is null) continue;
                    var s = IGalInfoPhraser.Similarity(name, itemName);
                    if (!(s > max)) continue;
                    max = s;
                    id = it["id"]?.ToObject<int>();
                    if (max > 0.999) return id;
                }
            }
            return id;
        }
        catch { return null; }
    }

    #endregion
}
