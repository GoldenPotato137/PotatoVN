using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IGalgameService
{
    public Task<Galgame?> GetGalgameAsync(int id);
    
    public Task<PagedResult<Galgame>> GetGalgamesAsync(int userId, long timestamp, int pageIndex, int pageSize);
    
    public Task<Galgame> AddOrUpdateGalgameAsync(int userId, GalgameUpdateDto galgame);
    
    public Task<Galgame?> AddPlayLogAsync(int userId, int galgameId, PlayLogDto payload);
    
    /// <exception cref="ArgumentException">galgame不存在</exception>
    /// <exception cref="UnauthorizedAccessException">userId与galgame拥有者不一致</exception>
    public Task DeleteGalgameAsync(int userId, int id);
    
    public Task DeleteGalgamesAsync(int userId);
    
    public Task<PagedResult<GalgameDeleted>> GetDeletedGalgamesAsync(int userId, long timestamp, int pageIndex, int pageSize);
}