using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            //不再跟踪插入失败的实体，避免同一个DbContext之后保存时再次失败
            context.Entry(user).State = EntityState.Detached;
            throw new DuplicateUserException(e.InnerException!.Message);
        }
        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        context.User.Update(user);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            //撤销本次修改，避免同一个DbContext之后保存时再次失败
            await context.Entry(user).ReloadAsync();
            throw new DuplicateUserException(e.InnerException!.Message);
        }
        return user;
    }

    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}