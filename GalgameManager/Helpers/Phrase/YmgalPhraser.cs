using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
// ReSharper disable ClassNeverInstantiated.Global

namespace GalgameManager.Helpers.Phrase;

public class YmgalPhraser: IGalInfoPhraser
{
    private HttpClient _httpClient;
    private static string _baseUrl = "https://www.ymgal.games/";
    private static string _publicClientId = "ymgal";
    private static string _publicClientSecret = "luna0327";

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
            url = _baseUrl + $"open/archive/?gid={id}";
        }
        catch (Exception)
        {
            url = _baseUrl + 
                  $"open/archive/search-game?mode=accurate&keyword={name}&similarity=50";
        }

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                ApiResponse<GameResponse>? apiResponse =
                    JsonConvert.DeserializeObject<ApiResponse<GameResponse>>(
                        await response.Content.ReadAsStringAsync(),
                        new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        });
                if (!apiResponse?.Success ?? apiResponse == null) return null;
                Game g = apiResponse.Data.Game;
                Galgame result = new()
                {
                    Name = g.Name,
                    CnName = g.ChineseName,
                    Description = g.Introduction,
                    ReleaseDate = IGalInfoPhraser.GetDateTimeFromString(g.ReleaseDate) ?? DateTime.MinValue, 
                };
                return result;
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
            _baseUrl +
            $"oauth/token?grant_type=client_credentials&client_id={_publicClientId}&client_secret={_publicClientSecret}&scope=public");
        request.EnsureSuccessStatusCode();
        return JsonConvert.
            DeserializeObject<OauthRequest>
                (await request.Content.ReadAsStringAsync(), 
                    new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver{NamingStrategy = new SnakeCaseNamingStrategy()}
                    })?.AccessToken ?? "";
    }
    
    private void GetHttpClient()
    {
        var accessToken = OauthGet().Result;
        _httpClient = Utils.GetDefaultHttpClient().WithApplicationJson();
        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        _httpClient.DefaultRequestHeaders.Add("version", "1");
    }
}

public class OauthRequest
{
    public required string AccessToken { get; set; }
    public required string TokenType { get; set; }
    public required int ExpiresIn { get; set; }
    public required string Scope { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public int Code { get; set; }
    public required string Msg { get; set; }
    public required T Data { get; set; }
}

public class Archive
{
    public int PublishVersion { get; set; }
    public required string PublishTime { get; set; }
    public int Publisher { get; set; }
    public required string Name { get; set; }
    public required string ChineseName { get; set; }
    public required ExtensionName[] ExtensionName { get; set; }
    public required string Introduction { get; set; }
    public required string State { get; set; }
    public required int Weights { get; set; }
    public required string MainImg { get; set; }
    public required MoreEntry[] MoreEntry { get; set; }
}

public class Game : Archive
{
    public int Gid { get; set; }
    public int DeveloperId { get; set; }
    public bool HaveChinese { get; set; }
    public required string TypeDesc { get; set; }
    public required string ReleaseDate { get; set; }
    public bool Restricted { get; set; }
    public required string Country { get; set; }
    public required Website[] Website { get; set; }
    public required Characters[] Characters { get; set; }
    public required Releases[] Releases { get; set; }
    public required Staff[] Staff { get; set; }
    public required string Type { get; set; }
    public bool Freeze { get; set; }
}

public class ExtensionName
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Desc { get; set; }
}

public class MoreEntry
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}

public class Website
{
    public required string Title { get; set; }
    public required string Link { get; set; }
}

public class Characters
{
    public int Cid { get; set; }
    public int CvId { get; set; }
    public int CharacterPosition { get; set; }
}

public class Releases
{
    public int Id { get; set; }
    public required string ReleaseName { get; set; }
    public required string RelatedLink { get; set; }
    public required string Platform { get; set; }
    public required string ReleaseDate { get; set; }
    public required string ReleaseLanguage { get; set; }
    public required string RestrictionLevel { get; set; }
}

public class Staff
{
    public int Sid { get; set; }
    public int Pid { get; set; }
    public required string EmpName { get; set; }
    public required string EmpDesc { get; set; }
    public required string JobName { get; set; }
}


public class GameResponse
{
    public required Game Game { get; set; }
    public required Dictionary<string, CidMapping> CidMapping { get; set; }
    public required Dictionary<string, PidMapping> PidMapping { get; set; }
}

public class CidMapping 
{
    public int Cid { get; set; }
    public required string Name { get; set; }
    public required string MainImg { get; set; }
    public required string State { get; set; }
    public bool Freeze { get; set; }
}

public class PidMapping
{
    public int Pid { get; set; }
    public required string Name { get; set; }
    public required string MainImg { get; set; }
    public required string State { get; set; }
    public bool Freeze { get; set; }
}




