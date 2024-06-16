using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace GalgameManager.Helpers.Phrase;

public class YmgalPhraser: IGalInfoPhraser
{
    private HttpClient _httpClient;
    private static string BaseUrl = "https://www.ymgal.games/";
    private static string PublicClientId = "ymgal";
    private static string PublicClientSecret = "luna0327";

    // private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings()
    // {
    //     ContractResolver = new DefaultContractResolver()
    //     {
    //         NamingStrategy = new SnakeCaseNamingStrategy()
    //     }
    // };
    

    public YmgalPhraser()
    {
        _httpClient = new HttpClient();
        GetHttpClient();
    }

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        var name = galgame.Name.Value ?? "";
        var url = "";
        int? id;
        try
        {
            if (galgame.RssType != RssType.Ymgal) throw new Exception();
            id = Convert.ToInt32(galgame.Id ?? "");
            url = BaseUrl + $"open/archive/?gid={id}";
        }
        catch (Exception)
        {
            url = BaseUrl + 
                  $"open/archive/search-game?mode=accurate&keyword={name}&similarity=50";
        }

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                ApiResponse<GameResponse>? apiResponse =
                    JsonConvert.DeserializeObject<ApiResponse<GameResponse>>(
                        await response.Content.ReadAsStringAsync());
                if (!apiResponse?.success ?? apiResponse == null) return null;
                Game g = apiResponse.data.game;
                Galgame result = new()
                {
                    Name = g.name,
                    CnName = g.chineseName,
                    Description = g.introduction
                };
            }
        }
        catch (Exception e)
        {
            return null;
        }
        return null;
    }

    public RssType GetPhraseType() => RssType.Ymgal;

    public async Task<string> OauthGet()
    {
        HttpResponseMessage request = await _httpClient.GetAsync(
            BaseUrl +
            $"oauth/token?grant_type=client_credentials&client_id={PublicClientId}&client_secret={PublicClientSecret}&scope=public");
        request.EnsureSuccessStatusCode();
        return JsonConvert.
            DeserializeObject<OauthRequest>
                (await request.Content.ReadAsStringAsync())?.access_token ?? "";
    }
    
    private void GetHttpClient()
    {
        var access_token = OauthGet().Result;
        _httpClient = Utils.GetDefaultHttpClient().WithApplicationJson();
        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + access_token);
        _httpClient.DefaultRequestHeaders.Add("version", "1");
    }
}

public class OauthRequest
{
    public string access_token { get; set; }
    public string token_type { get; set; }
    public int expires_in { get; set; }
    public string scope { get; set; }
}

public class ApiResponse<T>
{
    public bool success { get; set; }
    public int code { get; set; }
    public string msg { get; set; }
    public T data { get; set; }
}

public class Archive
{
    public int publishVersion { get; set; }
    public string publishTime { get; set; }
    public int publisher { get; set; }
    public string name { get; set; }
    public string chineseName { get; set; }
    public ExtensionName[] extensionName { get; set; }
    public string introduction { get; set; }
    public string state { get; set; }
    public int weights { get; set; }
    public string mainImg { get; set; }
    public MoreEntry[] moreEntry { get; set; }
}

public class Game : Archive
{
    public int gid { get; set; }
    public int developerId { get; set; }
    public bool haveChinese { get; set; }
    public string typeDesc { get; set; }
    public string releaseDate { get; set; }
    public bool restricted { get; set; }
    public string country { get; set; }
    public Website[] website { get; set; }
    public Characters[] characters { get; set; }
    public Releases[] releases { get; set; }
    public Staff[] staff { get; set; }
    public string type { get; set; }
    public bool freeze { get; set; }
}

public class ExtensionName
{
    public string name { get; set; }
    public string type { get; set; }
    public string desc { get; set; }
}

public class MoreEntry
{
    public string key { get; set; }
    public string value { get; set; }
}

public class Website
{
    public string title { get; set; }
    public string link { get; set; }
}

public class Characters
{
    public int cid { get; set; }
    public int cvId { get; set; }
    public int characterPosition { get; set; }
}

public class Releases
{
    public int id { get; set; }
    public string releaseName { get; set; }
    public string relatedLink { get; set; }
    public string platform { get; set; }
    public string releaseDate { get; set; }
    public string releaseLanguage { get; set; }
    public string restrictionLevel { get; set; }
}

public class Staff
{
    public int sid { get; set; }
    public int pid { get; set; }
    public string empName { get; set; }
    public string empDesc { get; set; }
    public string jobName { get; set; }
}


public class GameResponse
{
    public Game game { get; set; }
    public Dictionary<string, CidMapping> cidMapping { get; set; }
    public Dictionary<string, PidMapping> pidMapping { get; set; }
}

public class CidMapping 
{
    public int cid { get; set; }
    public string name { get; set; }
    public string mainImg { get; set; }
    public string state { get; set; }
    public bool freeze { get; set; }
}

public class PidMapping
{
    public int pid { get; set; }
    public string name { get; set; }
    public string mainImg { get; set; }
    public string state { get; set; }
    public bool freeze { get; set; }
}




