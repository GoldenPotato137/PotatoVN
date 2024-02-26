using GalgameManager.Server.Contracts;
using GalgameManager.Server.Enums;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class UserController(
    IUserService userService,
    IUserRepository userRepository,
    IBangumiService bgmService,
    IOssService ossService,
    ILogger<UserController> logger) : ControllerBase
{
    /// <summary>获取用户列表</summary>
    /// <remarks>
    /// <b>该命令需要管理员权限</b><br/>
    /// pageIndex从0开始<br/>
    /// 不返回用户的头像URL
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 20)
    {
        PagedResult<User> tmp = await userRepository.GetUsersAsync(pageIndex, pageSize);
        PagedResult<UserDto> result = new(tmp.Items.ToDtoList(u => new UserDto(u)), tmp.PageIndex, tmp.PageSize,
            tmp.Cnt);
        return Ok(result);
    }

    /// <summary>获取指定id的用户</summary>
    /// <remarks><b>该命令需要管理员权限</b></remarks>
    /// <response code="404">用户不存在</response>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> GetUser([FromRoute] int id)
    {
        User? user = await userRepository.GetUserAsync(id);
        if (user == null) return NotFound();
        UserDto dto = new(user);
        await dto.WithAvatarAsync(ossService);
        return Ok(dto);
    }

    /// <summary>获取自己的用户信息</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me([FromQuery] bool withAvatar = true)
    {
        var userId = this.GetUserId();
        User? user = await userRepository.GetUserAsync(userId);
        if (user == null) return NotFound();
        UserDto dto = new(user);
        if(withAvatar)
            await dto.WithAvatarAsync(ossService);
        return Ok(dto);
    }

    /// <summary>登录账户</summary>
    /// <remarks>若登录成功则返回jwtToken</remarks>
    /// <response code="200">登录成功，返回token</response>
    /// <response code="400">账户不存在或密码不正确</response>
    /// <response code="503">不允许使用账户密码登录与注册</response>
    [HttpPost("session")]
    public async Task<ActionResult<UserWithTokenDto>> LoginAsync([FromBody] UserLoginDto payload)
    {
        if(userService.IsDefaultLoginEnable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Default login is disabled.");
        User? user = await userRepository.GetUserAsync(payload.UserName);
        if (user is null || BCrypt.Net.BCrypt.Verify(payload.Password, user.PasswordHash) == false)
            return BadRequest("User not found or password incorrect.");
        var token = userService.GetToken(user);
        UserDto dto = new(user);
        await dto.WithAvatarAsync(ossService);
        return Ok(new UserWithTokenDto(dto, token, userService.GetExpiryDateFromToken(token)));
    }

    /// <summary>使用bgm账户登录/注册</summary>
    /// <remarks>如果该bgm账户没有potatoVN账号，则自动注册一个</remarks>
    /// <response code="200">用户信息与token</response>
    /// <response code="400">token无效</response>
    /// <response code="502">无法连接至bgm服务器</response>
    /// <response code="503">不允许使用bangumi注册与登录</response>
    [HttpPost("session/bgm")]
    public async Task<ActionResult<UserWithTokenDto>> LoginViaBgmAsync([FromBody] UserLoginViaBgmDto payload)
    {
        if(bgmService.IsLoginEnable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Login via bangumi is disabled.");
        BangumiAccount account;
        try
        {
            account = await bgmService.GetAccount(payload.BgmToken);
        }
        catch (Exception e)
        {
            if (e is InvalidAuthorizationCodeException)
                return BadRequest($"{e.Message}");
            logger.LogWarning(e, "Failed to get Bangumi account with token");
            return StatusCode(StatusCodes.Status502BadGateway, e.ToString());
        }
        User? user = await userRepository.GetUserByBangumiIdAsync(account.Id);
        if (user is null)
        {
            user = new User
            {
                UserName = $"_bgm_{account.UserName}",
                DisplayUserName = account.UserDisplayName,
                BangumiId = account.Id,
                Type = UserType.User,
                TotalSpace = ossService.SpacePerUser,
            };
            await userRepository.AddUserAsync(user);
        }
        var token = userService.GetToken(user);
        UserDto dto = new(user);
        await dto.WithAvatarAsync(ossService);
        return Ok(new UserWithTokenDto(dto, token, userService.GetExpiryDateFromToken(token)));
    }
    
    /// <summary>注册账户</summary>
    /// <remarks>若注册成功则返回用户信息与token</remarks>
    /// <response code="400">该用户名已被占用</response>
    /// <response code="503">不允许使用账户密码登录与注册</response>
    [HttpPost]
    public async Task<ActionResult<UserWithTokenDto>> RegisterAsync([FromBody] UserRegisterDto payload)
    {
        if(userService.IsDefaultLoginEnable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Default login is disabled.");
        User? user = await userRepository.GetUserAsync(payload.UserName);
        if (user != null) return BadRequest("User already exists.");
        user = new User
        {
            UserName = payload.UserName,
            DisplayUserName = payload.UserName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(payload.Password),
            Type = UserType.User,
            TotalSpace = ossService.SpacePerUser,
        };
        await userRepository.AddUserAsync(user);
        var token = userService.GetToken(user);
        return Ok(new UserWithTokenDto(new UserDto(user), token, userService.GetExpiryDateFromToken(token)));
    }

    /// <summary>修改用户信息</summary>
    /// <remarks>所有字段均可选</remarks>
    /// <response code="200">成功，返回新的用户信息</response>
    /// <response code="400">填入了新密码但旧密码不正确/没填写</response>
    [HttpPatch("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> ModifyAsync([FromBody] UserModifyDto payload)
    {
        User? user = await userRepository.GetUserAsync(this.GetUserId());
        if (user == null) return NotFound();
        user.DisplayUserName = payload.UserDisplayName ?? user.DisplayUserName;
        user.AvatarLoc = payload.AvatarLoc ?? user.AvatarLoc;
        if (payload.NewPassword is not null)
        {
            if(payload.OldPassword is null || BCrypt.Net.BCrypt.Verify(payload.OldPassword, user.PasswordHash) == false)
                return BadRequest("Old password incorrect.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(payload.NewPassword);
        }
        await userRepository.UpdateUserAsync(user);
        UserDto dto = new(user);
        await dto.WithAvatarAsync(ossService);
        return Ok(dto);
    }
}