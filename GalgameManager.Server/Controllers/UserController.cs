using GalgameManager.Server.Contracts;
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
        PagedResult<User> tmp = await userService.GetUsersAsync(pageIndex, pageSize);
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
        User? user = await userService.GetUserAsync(id);
        if (user == null) return NotFound();
        return Ok(await ToDtoAsync(user));
    }

    /// <summary>获取自己的用户信息</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me([FromQuery] bool withAvatar = true)
    {
        var userId = this.GetUserId();
        User? user = await userService.GetUserAsync(userId);
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
        try
        {
            User user = await userService.LoginAsync(payload.UserName, payload.Password);
            return Ok(await ToDtoWithTokenAsync(user));
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>使用bgm账户登录/注册</summary>
    /// <remarks>如果该bgm账户没有potatoVN账号，则自动注册一个</remarks>
    /// <response code="200">用户信息与token</response>
    /// <response code="400">token无效</response>
    /// <response code="409">自动注册时发生冲突，可重试</response>
    /// <response code="502">无法连接至bgm服务器</response>
    /// <response code="503">不允许使用bangumi注册与登录</response>
    [HttpPost("session/bgm")]
    public async Task<ActionResult<UserWithTokenDto>> LoginViaBgmAsync([FromBody] UserLoginViaBgmDto payload)
    {
        if(bgmService.IsLoginEnable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Login via bangumi is disabled.");
        try
        {
            User user = await userService.LoginViaBgmAsync(payload.BgmToken);
            return Ok(await ToDtoWithTokenAsync(user));
        }
        catch (InvalidAuthorizationCodeException e)
        {
            return BadRequest(e.Message);
        }
        catch (BangumiUnavailableException e)
        {
            logger.LogWarning(e, "Failed to get Bangumi account with token");
            return StatusCode(StatusCodes.Status502BadGateway, e.ToString());
        }
        catch (DuplicateUserException)
        {
            return Conflict("Failed to register, please try again.");
        }
    }

    /// <summary>注册账户</summary>
    /// <remarks>若注册成功则返回用户信息与token</remarks>
    /// <response code="400">该用户名已被占用，或用户名以保留前缀_bgm_开头</response>
    /// <response code="503">不允许使用账户密码登录与注册</response>
    [HttpPost]
    public async Task<ActionResult<UserWithTokenDto>> RegisterAsync([FromBody] UserRegisterDto payload)
    {
        if(userService.IsDefaultLoginEnable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Default login is disabled.");
        try
        {
            User user = await userService.RegisterAsync(payload.UserName, payload.Password);
            return Ok(await ToDtoWithTokenAsync(user));
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>获取一个新jwt token</summary>
    /// <remarks>获取一个当前用户的新token</remarks>
    [HttpGet("session/refresh")]
    [Authorize]
    public async Task<ActionResult<UserWithTokenDto>> RefreshTokenAsync()
    {
        var userId = this.GetUserId();
        User? user = await userService.GetUserAsync(userId);
        if (user == null) return NotFound();
        return Ok(await ToDtoWithTokenAsync(user));
    }

    /// <summary>修改用户信息</summary>
    /// <remarks>
    /// 所有字段均可选<br/>
    /// 若用户还没有设置密码（如通过第三方账户注册，见UserDto.HasPassword），设置密码时无需填写旧密码
    /// </remarks>
    /// <response code="200">成功，返回新的用户信息</response>
    /// <response code="400">新密码为空，或填入了新密码但旧密码不正确/没填写</response>
    [HttpPatch("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> ModifyAsync([FromBody] UserModifyDto payload)
    {
        try
        {
            User user = await userService.ModifyAsync(this.GetUserId(), payload);
            return Ok(await ToDtoAsync(user));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>为当前账户绑定bgm账户</summary>
    /// <remarks>绑定后可以使用该bgm账户登录当前账户</remarks>
    /// <response code="200">绑定成功，返回新的用户信息</response>
    /// <response code="400">token无效，或当前账户已绑定了其他bgm账户（需先解绑）</response>
    /// <response code="409">该bgm账户已被其他账户绑定</response>
    /// <response code="502">无法连接至bgm服务器</response>
    /// <response code="503">不允许使用bangumi注册与登录</response>
    [HttpPost("me/bgm")]
    [Authorize]
    public async Task<ActionResult<UserDto>> BindBgmAsync([FromBody] UserLoginViaBgmDto payload)
    {
        if(bgmService.IsLoginEnable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Login via bangumi is disabled.");
        try
        {
            User user = await userService.BindBgmAsync(this.GetUserId(), payload.BgmToken);
            return Ok(await ToDtoAsync(user));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidAuthorizationCodeException e)
        {
            return BadRequest(e.Message);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (BangumiUnavailableException e)
        {
            logger.LogWarning(e, "Failed to get Bangumi account with token");
            return StatusCode(StatusCodes.Status502BadGateway, e.ToString());
        }
        catch (DuplicateUserException)
        {
            return Conflict("This bangumi account is already bound to another user.");
        }
    }

    /// <summary>解绑当前账户的bgm账户</summary>
    /// <remarks>为防止解绑后无法登录，只有在设置了密码且服务器允许账户密码登录时才能解绑</remarks>
    /// <response code="200">解绑成功，返回新的用户信息</response>
    /// <response code="400">当前账户没有绑定bgm账户，或解绑后将没有其他登录方式</response>
    [HttpDelete("me/bgm")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UnbindBgmAsync()
    {
        try
        {
            User user = await userService.UnbindBgmAsync(this.GetUserId());
            return Ok(await ToDtoAsync(user));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
    }

    private async Task<UserDto> ToDtoAsync(User user)
    {
        UserDto dto = new(user);
        await dto.WithAvatarAsync(ossService);
        return dto;
    }

    private async Task<UserWithTokenDto> ToDtoWithTokenAsync(User user)
    {
        var token = userService.GetToken(user);
        return new UserWithTokenDto(await ToDtoAsync(user), token, userService.GetExpiryDateFromToken(token));
    }
}