using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Enums;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;
using Microsoft.IdentityModel.Tokens;

namespace GalgameManager.Server.Services;

public class UserService (IConfiguration config, IUserRepository repository, IBangumiService bgmService,
    IOssService ossService): IUserService
{
    /// <summary>通过bgm自动注册的账户的用户名前缀，普通注册不允许使用</summary>
    private const string BgmUserNamePrefix = "_bgm_";

    private readonly string _jwtKey = config["AppSettings:JwtKey"]!;

    public bool IsDefaultLoginEnable { get; } = Convert.ToBoolean(config["AppSettings:DefaultLoginEnable"] ?? "True");

    public string GetToken(User user)
    {
        List<Claim> claims = new()
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Type.ToString()),
        };
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwtKey));
        SigningCredentials cred = new(key, SecurityAlgorithms.HmacSha512Signature);
        JwtSecurityToken token = new(claims: claims, expires: DateTime.Now.AddMonths(1), signingCredentials: cred);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    public long GetExpiryDateFromToken(string token)
    {
        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token)) return 0;
        JwtSecurityToken? jwtToken = handler.ReadJwtToken(token);
        Claim? expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);
        if (expClaim != null && long.TryParse(expClaim.Value, out var expValue))
            return expValue;
        return 0;
    }

    public async Task UpdateLastModifiedAsync(int userId, long lastModifiedTimestamp)
    {
        User? user = await repository.GetUserAsync(userId);
        if (user is null) return;
        if (lastModifiedTimestamp > user.LastGalChangedTimeStamp)
        {
            user.LastGalChangedTimeStamp = lastModifiedTimestamp;
            await repository.UpdateUserAsync(user);
        }
    }

    public async Task<PagedResult<User>> GetUsersAsync(int pageIndex, int pageSize)
    {
        return await repository.GetUsersAsync(pageIndex, pageSize);
    }

    public async Task<User?> GetUserAsync(int id)
    {
        return await repository.GetUserAsync(id);
    }

    public async Task<User> LoginAsync(string userName, string password)
    {
        User? user = await repository.GetUserAsync(userName);
        //通过第三方账户注册且未设置密码的用户PasswordHash为空，BCrypt无法校验空hash
        if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
            BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) == false)
            throw new ArgumentException("User not found or password incorrect.");
        return user;
    }

    public async Task<User> LoginViaBgmAsync(string bgmToken)
    {
        BangumiAccount account = await GetBgmAccountAsync(bgmToken);
        User? user = await repository.GetUserByBangumiIdAsync(account.Id);
        if (user is not null) return user;
        try
        {
            return await repository.AddUserAsync(new User
            {
                UserName = await GetAvailableBgmUserNameAsync(account.UserName),
                DisplayUserName = account.UserDisplayName,
                BangumiId = account.Id,
                Type = UserType.User,
                TotalSpace = ossService.SpacePerUser,
            });
        }
        catch (DuplicateUserException)
        {
            //同一bgm账户并发首次登录时，另一个请求可能已经完成了注册
            user = await repository.GetUserByBangumiIdAsync(account.Id);
            if (user is null) throw;
            return user;
        }
    }

    public async Task<User> RegisterAsync(string userName, string password)
    {
        if (userName.StartsWith(BgmUserNamePrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"User name cannot start with '{BgmUserNamePrefix}'.");
        if (await repository.GetUserAsync(userName) is not null)
            throw new ArgumentException("User already exists.");
        try
        {
            return await repository.AddUserAsync(new User
            {
                UserName = userName,
                DisplayUserName = userName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Type = UserType.User,
                TotalSpace = ossService.SpacePerUser,
            });
        }
        catch (DuplicateUserException)
        {
            //检查后该用户名被并发注册
            throw new ArgumentException("User already exists.");
        }
    }

    public async Task<User> ModifyAsync(int userId, UserModifyDto payload)
    {
        User user = await GetUserOrThrowAsync(userId);
        if (payload.NewPassword is not null)
        {
            if (string.IsNullOrEmpty(payload.NewPassword))
                throw new ArgumentException("New password cannot be empty.");
            var hasPassword = string.IsNullOrEmpty(user.PasswordHash) == false;
            if (hasPassword && (payload.OldPassword is null ||
                                BCrypt.Net.BCrypt.Verify(payload.OldPassword, user.PasswordHash) == false))
                throw new ArgumentException("Old password incorrect.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(payload.NewPassword);
        }
        user.DisplayUserName = payload.UserDisplayName ?? user.DisplayUserName;
        user.AvatarLoc = payload.AvatarLoc ?? user.AvatarLoc;
        return await repository.UpdateUserAsync(user);
    }

    public async Task<User> BindBgmAsync(int userId, string bgmToken)
    {
        User user = await GetUserOrThrowAsync(userId);
        BangumiAccount account = await GetBgmAccountAsync(bgmToken);
        if (user.BangumiId == account.Id) return user;
        if (user.BangumiId != 0)
            throw new ArgumentException("Already bound to another bangumi account, unbind it first.");
        if (await repository.GetUserByBangumiIdAsync(account.Id) is not null)
            throw new DuplicateUserException("This bangumi account is already bound to another user.");
        user.BangumiId = account.Id;
        //检查后该bgm账户可能被并发绑定/注册，此时会抛出DuplicateUserException
        return await repository.UpdateUserAsync(user);
    }

    public async Task<User> UnbindBgmAsync(int userId)
    {
        User user = await GetUserOrThrowAsync(userId);
        if (user.BangumiId == 0)
            throw new ArgumentException("Not bound to any bangumi account.");
        //解绑后只能使用账户密码登录
        if (string.IsNullOrEmpty(user.PasswordHash) || IsDefaultLoginEnable == false)
            throw new ArgumentException("Cannot unbind: no other login method available.");
        user.BangumiId = 0;
        return await repository.UpdateUserAsync(user);
    }

    private async Task<User> GetUserOrThrowAsync(int userId)
    {
        return await repository.GetUserAsync(userId) ?? throw new KeyNotFoundException("User not found.");
    }

    private async Task<BangumiAccount> GetBgmAccountAsync(string bgmToken)
    {
        try
        {
            return await bgmService.GetAccount(bgmToken);
        }
        catch (Exception e) when (e is not InvalidAuthorizationCodeException)
        {
            throw new BangumiUnavailableException("Failed to get bangumi account.", e);
        }
    }

    /// <summary>
    /// 为bgm自动注册的账户生成未被占用的用户名，默认为_bgm_{bgm用户名}<br/>
    /// 若已被占用（如原账户已解绑bgm）则添加数字后缀
    /// </summary>
    private async Task<string> GetAvailableBgmUserNameAsync(string bgmUserName)
    {
        var baseName = $"{BgmUserNamePrefix}{bgmUserName}";
        var name = baseName;
        for (var i = 2; await repository.GetUserAsync(name) is not null; i++)
            name = $"{baseName}_{i}";
        return name;
    }
}