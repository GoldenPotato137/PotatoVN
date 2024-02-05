using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Enums;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class UserController(IUserRepository userRepository, IConfiguration config) : ControllerBase
{
    /// <summary>获取用户列表</summary>
    /// <remarks>
    /// <b>该命令需要管理员权限</b><br/>
    /// pageIndex从0开始
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 20)
    {
        PagedResult<User> tmp = await userRepository.GetUsersAsync(pageIndex, pageSize);
        PagedResult<UserDto> result = new(tmp.Items.ToDto(), tmp.PageIndex, tmp.PageSize, tmp.Cnt);
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
        return Ok(new UserDto(user));
    }

    /// <summary>获取自己的用户信息</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = this.GetUserId();
        User? user = await userRepository.GetUserAsync(userId);
        if (user == null) return NotFound();
        return Ok(new UserDto(user));
    }

    /// <summary>登录账户</summary>
    /// <remarks>若登录成功则返回jwtToken</remarks>
    /// <response code="200">登录成功，返回token</response>
    /// <response code="400">账户不存在或密码不正确</response>
    [HttpPost("session")]
    public async Task<ActionResult<string>> LoginAsync([FromQuery] UserLoginDto payload)
    {
        User? user = await userRepository.GetUserAsync(payload.UserName);
        if (user is null || BCrypt.Net.BCrypt.Verify(payload.Password, user.PasswordHash) == false)
            return BadRequest("User not found or password incorrect.");
        return Ok(GetToken(user));
    }

    /// <summary>注册账户</summary>
    /// <remarks>若注册成功则返回用户信息</remarks>
    /// <response code="400">该用户名已被占用</response>
    [HttpPost]
    public async Task<ActionResult<UserDto>> RegisterAsync([FromQuery] UserRegisterDto payload)
    {
        User? user = await userRepository.GetUserAsync(payload.UserName);
        if (user != null) return BadRequest("User already exists.");
        user = new User
        {
            UserName = payload.UserName,
            DisplayUserName = payload.UserName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(payload.Password),
            Type = UserType.User,
        };
        await userRepository.AddUserAsync(user);
        return Ok(new UserDto(user));
    }
    
    private string GetToken(User user)
    {
        List<Claim> claims = new()
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Type.ToString()),
        };
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(config.GetSection("AppSettings:JwtKey").Value!));
        SigningCredentials cred = new(key, SecurityAlgorithms.HmacSha512Signature);
        JwtSecurityToken token = new(claims: claims, expires: DateTime.Now.AddMonths(1), signingCredentials: cred);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}