using System.ComponentModel.DataAnnotations;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")] 
[ApiController]
public class BangumiController (IBangumiService bgmService, ILogger<UserController> logger): ControllerBase
{
    /// <summary>使用code换取bgm token</summary>
    /// <response code="200">成功，返回token信息</response>
    /// <response code="400">code无效</response>
    /// <response code="502">无法连接至bangumi服务器</response>
    /// <response code="503">Bangumi服务没有启用</response>
    [HttpGet("oauth")]
    public async Task<ActionResult<BangumiToken>> OAuthCallback([Required] string code)
    {
        if(bgmService.IsOauth2Enable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Bangumi service is disabled.");
        if(string.IsNullOrEmpty(code))
            return BadRequest("Code is required.");
        try
        {
            BangumiToken token = await bgmService.GetTokenWithCodeAsync(code);
            return Ok(token);
        }
        catch (InvalidAuthorizationCodeException e)
        {
            logger.LogInformation(e, "Invalid authorization code");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get Bangumi account with code");
            return StatusCode(StatusCodes.Status502BadGateway, e.ToString());
        }
    }

    /// <summary>使用refresh token换取bgm token</summary>
    /// <response code="200">成功，返回token信息</response>
    /// <response code="400">code无效</response>
    /// <response code="502">无法连接至bangumi服务器</response>
    /// <response code="503">Bangumi服务没有启用</response>
    [HttpGet("refresh")]
    public async Task<ActionResult<BangumiToken>> RefreshToken([Required] string refreshToken)
    {
        if(bgmService.IsOauth2Enable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Bangumi service is disabled.");
        if(string.IsNullOrEmpty(refreshToken))
            return BadRequest("Code is required.");
        try
        {
            BangumiToken token = await bgmService.GetTokenWithRefreshTokenAsync(refreshToken);
            return Ok(token);
        }
        catch (InvalidAuthorizationCodeException e)
        {
            logger.LogInformation(e, "Invalid authorization code");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to get Bangumi account with code");
            return StatusCode(StatusCodes.Status502BadGateway, e.ToString());
        }
    }
}