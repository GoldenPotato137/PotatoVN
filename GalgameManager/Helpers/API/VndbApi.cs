using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;

namespace GalgameManager.Helpers.API;

public interface IVndbApi
{
    [Post("/vn")]
    public Task<VndbResponse<VndbVn>> GetVisualNovelAsync([Body]VndbQuery vndbQuery);

    [Post("/character")]
    public Task<VndbResponse<VndbCharacter>> GetVnCharacterAsync([Body]VndbQuery vndbQuery);

    // [Headers("Authorization: Token")] 用于标记，以便插入header
    [Headers("Authorization: Token")]
    [Get("/ulist_labels?user={id}")]
    public Task<UserLabelsResponse> GetUserLabelsAsync(string id);
    
    [Headers("Authorization: Token")]
    [Get("/ulist_labels")]
    public Task<UserLabelsResponse> GetCurrentUserLabelsAsync(string id);

    [Headers("Authorization: Token")]
    [Get("/authinfo")]
    public Task<AuthInfoResponse> GetAuthInfo();
    
    [Headers("Authorization: Token")]
    [Post("/ulist")]
    public Task<VndbResponse<VndbUserListItem>> GetUserVisualNovelListAsync([Body] VndbQuery vndbQuery);

    [Headers("Authorization: Token")]
    [Patch("/ulist/{id}")]
    public Task<ApiResponse<object>> ModifyUserVnAsync(string id, [Body] PatchUserListRequest patchUserListRequest);
    
    [Headers("Authorization: Token")]
    [Delete("/ulist/{id}")]
    public Task<ApiResponse<object>> DeleteUserVnAsync(string id);
}

public class VndbApi : IVndbApi
{
    private readonly IVndbApi _vndbApiImplementation;
    private readonly VndbAuthorizationHandler _vndbAuthorizationHandler;
    public async Task<VndbResponse<VndbVn>> GetVisualNovelAsync(VndbQuery vndbQuery) => 
        await _vndbApiImplementation.GetVisualNovelAsync(vndbQuery);

    public async Task<VndbResponse<VndbCharacter>> GetVnCharacterAsync(VndbQuery vndbQuery) => 
        await _vndbApiImplementation.GetVnCharacterAsync(vndbQuery);

    public async Task<UserLabelsResponse> GetUserLabelsAsync(string id) => 
        await _vndbApiImplementation.GetUserLabelsAsync(id);

    public async Task<UserLabelsResponse> GetCurrentUserLabelsAsync(string id) => 
        await _vndbApiImplementation.GetCurrentUserLabelsAsync(id);

    public async Task<AuthInfoResponse> GetAuthInfo() => 
        await _vndbApiImplementation.GetAuthInfo();

    public async Task<VndbResponse<VndbUserListItem>> GetUserVisualNovelListAsync(VndbQuery vndbQuery) => 
        await _vndbApiImplementation.GetUserVisualNovelListAsync(vndbQuery);

    public async Task<ApiResponse<object>> ModifyUserVnAsync(string id, PatchUserListRequest patchUserListRequest) => 
        await _vndbApiImplementation.ModifyUserVnAsync(id, patchUserListRequest);

    public async Task<ApiResponse<object>> DeleteUserVnAsync(string id) => 
        await _vndbApiImplementation.DeleteUserVnAsync(id);
    
    public void UpdateToken(string? token)
    {
        _vndbAuthorizationHandler.UpdateToken(token);
    }

    public VndbApi(string? token=null)
    {
        _vndbAuthorizationHandler = new VndbAuthorizationHandler(token);
        _vndbApiImplementation = RestService.For<IVndbApi>("https://api.vndb.org/kana",
            new RefitSettings
            {
                ContentSerializer = new NewtonsoftJsonContentSerializer(
                    new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver
                        {
                            NamingStrategy = new SnakeCaseNamingStrategy()
                        },
                    }),
                ExceptionFactory = ExceptionFactory,
                HttpMessageHandlerFactory = () => _vndbAuthorizationHandler
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

/// <summary>
/// Code:429
/// </summary>
public class ThrottledException : Exception
{
    public ThrottledException() : base("Throttled")
    {
        
    }
}

/// <summary>
/// Code:401
/// </summary>
public class InvalidTokenException : Exception
{
    public InvalidTokenException() : base("Invalid authentication token.")
    {
        
    }
}

internal class VndbAuthorizationHandler : DelegatingHandler
{
    private string? _token;

    public VndbAuthorizationHandler(string? token=null): base(new HttpClientHandler())
    {
        _token = token;
    }

    public void UpdateToken(string? token)
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