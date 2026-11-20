using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IUserRepository
{
    /// <summary>
    /// 获取用户列表
    /// </summary>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public Task<PagedResult<User>> GetUsersAsync(int pageIndex, int pageSize);
    
    /// <summary>
    /// 获取用户，若不存在则返回null
    /// </summary>;
    public Task<User?> GetUserAsync(int id);
    
    /// <summary>
    /// 获取用户，若不存在则返回null
    /// </summary>
    public Task<User?> GetUserAsync(string username);
    
    /// <summary>
    /// 获取用户，若不存在则返回null
    /// </summary>
    public Task<User?> GetUserByBangumiIdAsync(int bangumiId);
    
    /// <summary>
    /// 添加用户
    /// </summary>
    /// <exception cref="DuplicateUserException">用户名或BangumiId与已有用户重复</exception>
    public Task<User> AddUserAsync(User user);

    /// <summary>
    /// 更新用户信息
    /// </summary>
    /// <exception cref="DuplicateUserException">用户名或BangumiId与已有用户重复，此时user会被恢复为数据库中的值</exception>
    public Task<User> UpdateUserAsync(User user);
}