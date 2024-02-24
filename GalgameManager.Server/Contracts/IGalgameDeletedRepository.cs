using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IGalgameDeletedRepository
{
    public Task<PagedResult<GalgameDeleted>> GetGalgameDeletedsAsync(int userId, long timestamp, int pageIndex,
        int pageSize);
    
    public Task<GalgameDeleted> AddGalgameDeletedAsync(GalgameDeleted galgameDeleted);
}