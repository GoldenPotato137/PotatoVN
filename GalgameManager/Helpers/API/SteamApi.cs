using System.Net;
using System.Net.Http.Headers;
using GalgameManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;

namespace GalgameManager.Helpers.API;

public interface ISteamApi
{

    [Get("ISteamUser/GetPlayerSummaries/v0002/?key={key}&steamids={steamids}")]
    Task<ApiResponse<SteamAccount>> GetPlayerSummariesAsync(string key, string steamids);

    [Get("IPlayerService/GetOwnedGames/v0001/?key={key}&steamid={steamid}&include_appinfo={include_appinfo}")]
    Task<ApiResponse<AllSteamList>> GetOwnedGamesAsync(string key, string steamid, bool include_appinfo = true);


}
public class SteamApi : ISteamApi
{
    private readonly ISteamApi _steamApiImplementation;


    public async Task<ApiResponse<AllSteamList>> GetOwnedGamesAsync(string key, string steamid, bool include_appinfo = true) => await _steamApiImplementation.GetOwnedGamesAsync(key, steamid);
    public async Task<ApiResponse<SteamAccount>> GetPlayerSummariesAsync(string key, string steamids) => await _steamApiImplementation.GetPlayerSummariesAsync(key, steamids);



    public SteamApi()
    {
        _steamApiImplementation = RestService.For<ISteamApi>("http://api.steampowered.com/", new RefitSettings
        {
            // });
            ContentSerializer = new NewtonsoftJsonContentSerializer(
                    new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver
                        {
                            NamingStrategy = new SnakeCaseNamingStrategy()
                        },
                    }),
            ExceptionFactory = ExceptionFactory,
        });

    }
    private static async Task<Exception?> ExceptionFactory(HttpResponseMessage arg)
    {
        await Task.CompletedTask;
        return arg.StatusCode switch
        {
            HttpStatusCode.BadRequest => new HttpRequestException(arg.Content.ToString()),
            HttpStatusCode.TooManyRequests => new ThrottledException(),
            HttpStatusCode.Unauthorized => new InvalidTokenException(),
            _ => null
        };
    }
}
internal class SteamAuthorizationHandler : DelegatingHandler
{
    private string? _token;

    public SteamAuthorizationHandler(string token)
    {
        _token = token;
    }
    public void UpdateToken(string token)
    {
        _token = token;
    }
    protected async override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        // See if the request has an authorize header
        AuthenticationHeaderValue? auth = request.Headers.Authorization;
        if (auth != null && !_token.IsNullOrEmpty())
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(auth.Scheme, _token);
        }
        else
        {
            request.Headers.Authorization = null;
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}