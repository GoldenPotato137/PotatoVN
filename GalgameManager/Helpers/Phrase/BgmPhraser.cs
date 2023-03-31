#nullable enable
using System.Net.Http.Headers;
using System.Web;

using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;

using Newtonsoft.Json.Linq;

// ReSharper disable EnforceIfStatementBraces

namespace GalgameManager.Helpers.Phrase;

public class BgmPhraser : IGalInfoPhraser
{
    private readonly HttpClient _httpClient;

    public BgmPhraser(ILocalSettingsService localSettingsService)
    {
        var bgmToken = localSettingsService.ReadSettingAsync<string>(KeyValues.BangumiToken).Result;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "GoldenPotato/GalgameManager/1.0-dev (Windows) (no on github yet)");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if(bgmToken != null)
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + bgmToken);
    }
    
    private async Task<int?> GetId(string name)
    {
        try
        {
            var url = "https://api.bgm.tv/search/subject/" + HttpUtility.UrlEncode(name) + "?type=4";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var tmp = await response.Content.ReadAsStringAsync();
            var jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            var games = jsonToken["list"]!.ToObject<List<JToken>>();
            if (games==null || games.Count == 0) return null;
            return games[0]["id"]!.ToObject<int>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    #region tempDisable

    // private async Task<int?> SendPostRequestAsync()
    // {
    //     try
    //     {
    //         var url = "https://api.bgm.tv/v0/search/subjects?";
    //         var keyword = "糖调！-sugarfull tempering-";
    //         int[] typeFilter = { 4 };
    //         var requestData = new
    //         {
    //             keyword,
    //             filter = new
    //             {
    //                 type = typeFilter
    //             }
    //         };
    //         // _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
    //         var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
    //         var response = await _httpClient.PostAsync(url, content);
    //         if (!response.IsSuccessStatusCode)
    //             return null;
    //
    //         var jToken = JToken.Parse(await response.Content.ReadAsStringAsync());
    //         var games = jToken["data"];
    //         if (games[0] != null)
    //             return games[0]["id"].ToObject<int>();
    //
    //         return null;
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine(e);
    //         throw;
    //     }
    // }

    #endregion
    
    public async Task<Galgame?> GetGalgameInfo(string name)
    {
        var id = await GetId(name);
        if (id == null) return null;
        var url = "https://api.bgm.tv/v0/subjects/" + id;
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        
        var jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());

        Galgame result = new();
        // name
        result.Name = jsonToken["name"]!.ToObject<string>()!;
        // description
        result.Description = jsonToken["summary"]!.ToObject<string>()!;
        // developer
        var infoBox = jsonToken["infobox"]!.ToObject<List<JToken>>()!;
        var developerInfoBox = infoBox.Find(x => x["key"]!.ToObject<string>()!.Contains("开发"));
        result.Developer = developerInfoBox==null?null:developerInfoBox["value"]!.ToObject<string>()!;
        return result;
    }

    public RssType GetPhraseType() => RssType.Bangumi;
}