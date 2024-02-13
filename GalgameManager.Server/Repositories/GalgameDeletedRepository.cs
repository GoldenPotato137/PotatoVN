using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Server.Repositories;

public class GalgameDeletedRepository(DataContext context) : IGalgameDeletedRepository
{
    public async Task<PagedResult<GalgameDeleted>> GetGalgameDeletedsAsync(int userId, long timestamp, int pageIndex,
        int pageSize)
    {
        IQueryable<GalgameDeleted> query =
            context.GalgameDeleted.Where(g => g.UsedId == userId && g.DeleteTimeStamp > timestamp);
        Task<int> countTask = query.CountAsync();
        Task<List<GalgameDeleted>> resultTask = query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<GalgameDeleted>(await resultTask, pageIndex, pageSize, await countTask);
    }

    public async Task<GalgameDeleted> AddGalgameDeletedAsync(GalgameDeleted galgameDeleted)
    {
        await context.GalgameDeleted.AddAsync(galgameDeleted);
        await context.SaveChangesAsync();
        return galgameDeleted;
    }
}