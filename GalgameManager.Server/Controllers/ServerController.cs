using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class ServerController (IUserService userService, IBangumiService bgmService): ControllerBase
{
    /// <summary>获取服务器信息</summary>
    [HttpGet("info")]
    public async Task<ActionResult<ServerInfoDto>> GetServerInfo()
    {
        await Task.CompletedTask; //之后添加别的逻辑会涉及到异步操作
        return Ok(new ServerInfoDto
        {
            BangumiOAuth2Enable = bgmService.IsOauth2Enable,
            DefaultLoginEnable = userService.IsDefaultLoginEnable,
            BangumiLoginEnable = bgmService.IsLoginEnable
        });
    }
}