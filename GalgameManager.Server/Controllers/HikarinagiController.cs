using System.ComponentModel.DataAnnotations;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class HikarinagiController(IHikarinagiService hikarinagiService, ILogger<HikarinagiController> logger)
    : ControllerBase
{
    /// <summary>生成Hikarinagi用户授权地址</summary>
    /// <response code="302">跳转至Hikarinagi ID授权页</response>
    /// <response code="400">PKCE或state参数无效</response>
    /// <response code="503">Hikarinagi OAuth服务没有启用</response>
    [HttpGet("authorize")]
    public IActionResult Authorize([Required] string state, [Required] string codeChallenge)
    {
        if (hikarinagiService.IsOAuth2Enable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Hikarinagi OAuth service is disabled.");
        try
        {
            return Redirect(hikarinagiService.GetAuthorizationUrl(state, codeChallenge));
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>使用授权码和PKCE verifier换取Hikarinagi用户令牌</summary>
    /// <response code="200">成功，返回访问令牌和刷新令牌</response>
    /// <response code="400">授权码或PKCE verifier无效</response>
    /// <response code="502">无法连接至Hikarinagi ID</response>
    /// <response code="503">Hikarinagi OAuth服务没有启用</response>
    [HttpPost("oauth")]
    public async Task<ActionResult<HikarinagiToken>> OAuth([FromBody] HikarinagiOAuthCodeRequest request)
    {
        if (hikarinagiService.IsOAuth2Enable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Hikarinagi OAuth service is disabled.");
        try
        {
            return Ok(await hikarinagiService.GetUserTokenWithCodeAsync(request.Code, request.CodeVerifier));
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            logger.LogInformation(e, "Invalid Hikarinagi authorization code or PKCE verifier");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to exchange Hikarinagi authorization code");
            return StatusCode(StatusCodes.Status502BadGateway,
                "Hikarinagi OAuth service is temporarily unavailable.");
        }
    }

    /// <summary>使用refresh token换取新的Hikarinagi用户令牌</summary>
    /// <remarks>Hikarinagi refresh token每次使用后都会轮换，客户端必须保存响应中的新值。</remarks>
    [HttpPost("refresh")]
    public async Task<ActionResult<HikarinagiToken>> Refresh([FromBody] HikarinagiRefreshTokenRequest request)
    {
        if (hikarinagiService.IsOAuth2Enable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Hikarinagi OAuth service is disabled.");
        try
        {
            return Ok(await hikarinagiService.GetUserTokenWithRefreshTokenAsync(request.RefreshToken));
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            logger.LogInformation(e, "Invalid Hikarinagi refresh token");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to refresh Hikarinagi user token");
            return StatusCode(StatusCodes.Status502BadGateway,
                "Hikarinagi OAuth service is temporarily unavailable.");
        }
    }
}

public class HikarinagiOAuthCodeRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string CodeVerifier { get; set; } = string.Empty;
}

public class HikarinagiRefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
