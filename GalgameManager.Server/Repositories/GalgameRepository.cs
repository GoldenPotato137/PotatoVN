using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Server.Repositories;

public class GalgameRepository (DataContext context): IGalgameRepository
{
    public async Task<Galgame?> GetGalgameAsync(int id, bool includePlayTime = false)
    {
        IQueryable<Galgame> query = context.Galgame.AsQueryable();
        if(includePlayTime)
            query = query.Include(g => g.PlayTime);
        return await query.FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<PagedResult<Galgame>> GetGalgamesAsync(int userId, long timestamp, int pageIndex, int pageSize)
    {
        if(pageIndex < 0 || pageSize < 0)
            throw new ArgumentException("Invalid page index or page size");
        
        IQueryable<Galgame> query = context.Galgame
            .Where(g => g.UserId == userId && g.LastChangedTimeStamp > timestamp);
        var count = await query.CountAsync();
        List<Galgame> data = await query
            .Include(g => g.PlayTime)
            .OrderByDescending(g => g.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return new PagedResult<Galgame>(data, pageIndex, pageSize, count);
    }

    public async Task<Galgame> AddGalgameAsync(Galgame galgame)
    {
        await context.Galgame.AddAsync(galgame);
        await context.SaveChangesAsync();
        return galgame;
    }

    public async Task AddOrUpdateGalgameAsync(Galgame galgame)
    {
        context.Galgame.Update(galgame);
        await context.SaveChangesAsync();
    }

    public async Task DeleteGalgameAsync(int id)
    {
        Galgame? galgame = await context.Galgame.FindAsync(id);
        if (galgame is not null)
        {
            context.Galgame.Remove(galgame);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<int>> DeleteGalgamesAsync(int userId)
    {
        IQueryable<Galgame> query = context.Galgame.Where(g => g.UserId == userId);
        List<int> ids = await query.Select(g => g.Id).ToListAsync();
        await query.ExecuteDeleteAsync();
        return ids;
    }
}