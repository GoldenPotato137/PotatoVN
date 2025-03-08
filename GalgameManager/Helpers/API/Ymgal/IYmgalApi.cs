using Refit;

namespace GalgameManager.Helpers.API.Ymgal;

public interface IYmgalApi
{
    [Get("/open/archive/search-game")]
    Task<ApiResponse<Page<Game>>> SearchGameAsync(string keyword, int pageNum = 1, int pageSize = 20, string mode = "list");
    
    [Get("/open/archive")]
    Task<ApiResponse<GameResponse>> GetGameAsync(int gid);
    
    [Get("/open/archive/game")]
    Task<ApiResponse<OrganizationResponse>> GetOrganizationAsync(int orgId);

    [Get("/open/archive")]
    Task<ApiResponse<CharacterResponse>> GetCharacterAsync(int cid);

    [Get("/open/archive")]
    Task<ApiResponse<StaffResponse>> GetStaffAsync(int pid);
    
    [Get("/oauth/token")]
    Task<OauthRequest> GetOauthTokenAsync(string grant_type, string client_id, string client_secret, string scope);
}