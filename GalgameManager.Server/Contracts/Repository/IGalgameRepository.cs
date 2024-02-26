using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IGalgameRepository
{
    public Task<Galgame?> GetGalgameAsync(int id, bool includePlayTime = false);
    
    /// <summary>
    /// 获取指定用户的最后一次更新时间在指定时间戳之后（严格大于）的Galgame列表
    /// </summary>
    public Task<PagedResult<Galgame>> GetGalgamesAsync(int userId, long timestamp, int pageIndex, int pageSize);
    
    public Task<Galgame> AddGalgameAsync(Galgame galgame);
    
    public Task AddOrUpdateGalgameAsync(Galgame galgame);
    
    public Task DeleteGalgameAsync(int id);
    
    public Task<List<int>> DeleteGalgamesAsync(int userId);
}