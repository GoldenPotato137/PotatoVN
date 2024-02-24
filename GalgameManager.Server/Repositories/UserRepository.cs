using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Server.Repositories;

public class UserRepository(DataContext context) : IUserRepository
{
    public async Task<PagedResult<User>> GetUsersAsync(int pageIndex, int pageSize)
    {
        return new PagedResult<User>(await 
            context.User.OrderBy(u => u.Id).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync(), 
            pageIndex, pageSize, await context.User.CountAsync());
    }

    public async Task<User?> GetUserAsync(int id)
    {
        return await context.User.FindAsync(id);
    }

    public async Task<User?> GetUserAsync(string username)
    {
        return await context.User.FirstOrDefaultAsync(u => u.UserName == username);
    }

    public Task<User?> GetUserByBangumiIdAsync(int bangumiId)
    {
        return context.User.FirstOrDefaultAsync(u => u.BangumiId == bangumiId);
    }

    public async Task<User> AddUserAsync(User user)
    {
        context.User.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        context.User.Update(user);
        await context.SaveChangesAsync();
        return user;
    }
}