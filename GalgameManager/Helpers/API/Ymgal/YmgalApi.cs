using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;

namespace GalgameManager.Helpers.API.Ymgal;

public static class YmgalApi
{
    private static readonly string BaseUrl = "https://www.ymgal.games/";
    private static readonly string PublicClientId = "ymgal";
    private static readonly string PublicClientSecret = "luna0327";
    
    public static IYmgalApi GetApi(string? accessToken = null)
    {
        HttpClient client = Utils.GetDefaultHttpClient();
        
        if (!string.IsNullOrEmpty(accessToken))
        {
            client = client.WithApplicationJson();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            client.DefaultRequestHeaders.Add("version", "1");
        }

        client.BaseAddress = new Uri(BaseUrl);
        
        return RestService.For<IYmgalApi>(client, new RefitSettings
        {
            ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }),
        });
    }
    
    public static async Task<IYmgalApi> GetAuthenticatedApiAsync()
    {
        var unauthenticatedApi = GetApi();
        var tokenResponse = await unauthenticatedApi.GetOauthTokenAsync(
            "client_credentials", 
            PublicClientId, 
            PublicClientSecret, 
            "public");
        
        return GetApi(tokenResponse.Access_token);
    }
}