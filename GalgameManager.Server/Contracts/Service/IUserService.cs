using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IUserService
{
    public bool IsDefaultLoginEnable { get; }

    public string GetToken(User user);

    public long GetExpiryDateFromToken(string token);

    public Task UpdateLastModifiedAsync(int userId, long lastModifiedTimestamp);

    /// <summary>
    /// 获取用户列表
    /// </summary>
    public Task<PagedResult<User>> GetUsersAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 获取用户，若不存在则返回null
    /// </summary>
    public Task<User?> GetUserAsync(int id);

    /// <summary>
    /// 使用用户名与密码登录
    /// </summary>
    /// <exception cref="ArgumentException">用户不存在或密码不正确</exception>
    public Task<User> LoginAsync(string userName, string password);

    /// <summary>
    /// 使用bgm账户登录，若该bgm账户没有对应的用户则自动注册一个
    /// </summary>
    /// <exception cref="InvalidAuthorizationCodeException">bgm token无效</exception>
    /// <exception cref="BangumiUnavailableException">无法从bgm获取账户信息</exception>
    /// <exception cref="DuplicateUserException">自动注册时发生冲突，可重试</exception>
    public Task<User> LoginViaBgmAsync(string bgmToken);

    /// <summary>
    /// 注册账户
    /// </summary>
    /// <exception cref="ArgumentException">用户名已被占用，或以保留前缀_bgm_开头</exception>
    public Task<User> RegisterAsync(string userName, string password);

    /// <summary>
    /// 修改用户信息，payload中为null的字段不修改
    /// </summary>
    /// <exception cref="KeyNotFoundException">用户不存在</exception>
    /// <exception cref="ArgumentException">新密码为空，或用户已设置密码但旧密码不正确</exception>
    public Task<User> ModifyAsync(int userId, UserModifyDto payload);

    /// <summary>
    /// 为用户绑定bgm账户，若已绑定该bgm账户则什么都不做
    /// </summary>
    /// <exception cref="KeyNotFoundException">用户不存在</exception>
    /// <exception cref="InvalidAuthorizationCodeException">bgm token无效</exception>
    /// <exception cref="BangumiUnavailableException">无法从bgm获取账户信息</exception>
    /// <exception cref="ArgumentException">用户已绑定其他bgm账户</exception>
    /// <exception cref="DuplicateUserException">该bgm账户已被其他用户绑定</exception>
    public Task<User> BindBgmAsync(int userId, string bgmToken);

    /// <summary>
    /// 解绑用户的bgm账户
    /// </summary>
    /// <exception cref="KeyNotFoundException">用户不存在</exception>
    /// <exception cref="ArgumentException">用户没有绑定bgm账户，或解绑后将没有其他登录方式</exception>
    public Task<User> UnbindBgmAsync(int userId);
}