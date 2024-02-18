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
    public Task<User> AddUserAsync(User user);
    
    /// <summary>
    /// 更新用户信息
    /// </summary>
    public Task<User> UpdateUserAsync(User user);
}