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
            context.GalgameDeleted.Where(g => g.UserId == userId && g.DeleteTimeStamp > timestamp);
        var count = await query.CountAsync();
        List<GalgameDeleted> result = await query
            .Skip(pageIndex * pageSize).Take(pageSize).OrderBy(g => g.Id)
            .ToListAsync();
        return new PagedResult<GalgameDeleted>(result, pageIndex, pageSize, count);
    }

    public async Task<GalgameDeleted> AddGalgameDeletedAsync(GalgameDeleted galgameDeleted)
    {
        await context.GalgameDeleted.AddAsync(galgameDeleted);
        await context.SaveChangesAsync();
        return galgameDeleted;
    }
}