using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Helpers.API;
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
    private Task? _getOAuthTask;

    private readonly JsonSerializerSettings _snakeCaseSerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    };

    private readonly JsonSerializerSettings _camelCaseSerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
    

    public YmgalPhraser()
    {
        _httpClient = Utils.GetDefaultHttpClient().WithApplicationJson();;
        GetHttpClient();
    }

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (_getOAuthTask != null) await _getOAuthTask;
        var name = galgame.Name.Value ?? "";
        int? id;
        try
        {
            if (galgame.RssType != RssType.Ymgal) throw new Exception();
            id = Convert.ToInt32(galgame.Id ?? "");
            
        }
        catch (Exception)
        {
            var url = _baseUrl + 
                  $"open/archive/search-game?mode=list&keyword={name}&pageNum=1&pageSize=20";
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            ApiResponse<Page<Game>>? gameResponse =
                JsonConvert.DeserializeObject<ApiResponse<Page<Game>>>(
                    await response.Content.ReadAsStringAsync(),
                    _camelCaseSerializerSettings
                );
            if (gameResponse?.Data?.Result.Count != 0) id = gameResponse?.Data?.Result[0].Id;
            else return null;
        }
        

        try
        {
            var url = _baseUrl + $"open/archive/?gid={id}";
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                ApiResponse<GameResponse>? gameResponse =
                    JsonConvert.DeserializeObject<ApiResponse<GameResponse>>(
                        await response.Content.ReadAsStringAsync(),
                        _camelCaseSerializerSettings
                    );
                if (gameResponse?.Data?.Game is { } g){
                    Galgame result = new()
                    {
                        Name = g.Name,
                        CnName = g.ChineseName ?? "",
                        Description = g.Introduction,
                        ReleaseDate = IGalInfoPhraser.GetDateTimeFromString(g.ReleaseDate) ?? DateTime.MinValue, 
                        ImageUrl = g.MainImg,
                        Id = g.Gid != 0 ? g.Gid.ToString() : g.Id.ToString()
                    };
                    var organizationUrl = _baseUrl + $"open/archive/?orgId={g.DeveloperId}";
                    HttpResponseMessage dResponse = await _httpClient.GetAsync(organizationUrl);
                    if (dResponse.IsSuccessStatusCode)
                    {
                        ApiResponse<OrganizationResponse>? developerResponse =
                            JsonConvert.DeserializeObject<ApiResponse<OrganizationResponse>>(
                                await dResponse.Content.ReadAsStringAsync(),
                                _camelCaseSerializerSettings
                            );
                        result.Developer = developerResponse?.Data?.Org.Name ?? Galgame.DefaultString;
                    }
                    return result;
                }
            }
            
        }
        catch (Exception)
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
                (await request.Content.ReadAsStringAsync(), _snakeCaseSerializerSettings)?.AccessToken ?? "";
    }
    
    private void GetHttpClient()
    {
        _getOAuthTask = Task.Run(async () =>
        {
            var accessToken = await OauthGet();
            _httpClient = Utils.GetDefaultHttpClient().WithApplicationJson();
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
            _httpClient.DefaultRequestHeaders.Add("version", "1");
        });
    }
}







