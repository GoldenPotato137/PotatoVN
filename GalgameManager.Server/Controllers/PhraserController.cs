using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")]
[ApiController]
[EnableRateLimiting(Program.PhraserRateLimitPolicy)]
public class PhraserController(IHikarinagiService hikarinagiService, ILogger<PhraserController> logger): ControllerBase
{
    /// <summary>透传请求至Hikarinagi开放API</summary>
    /// <remarks>路径与查询字符串原样转发至 https://www.hikarinagi.org/api/v3/open/ ，自动附加访问令牌，响应原样返回。仅支持GET。<br/>
    /// 无需登录；未登录用户按IP限速360次/分钟，登录用户不限速。</remarks>
    /// <param name="path">API路径，如 galgames/1 或 search</param>
    /// <response code="200">成功，原样返回Hikarinagi响应</response>
    /// <response code="400">路径无效</response>
    /// <response code="429">请求过于频繁（未登录用户限速360次/分钟）</response>
    /// <response code="502">无法连接至Hikarinagi服务器</response>
    /// <response code="503">Hikarinagi服务没有启用</response>
    [HttpGet("hikarinagi/{**path}")]
    public async Task<IActionResult> HikarinagiProxy(string? path)
    {
        if(hikarinagiService.IsEnable == false)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Hikarinagi service is disabled.");
        try
        {
            ScraperProxyResult result =
                await hikarinagiService.ProxyAsync(path ?? string.Empty, Request.QueryString.Value ?? string.Empty);
            return new ContentResult
            {
                Content = result.Body,
                ContentType = result.ContentType,
                StatusCode = result.StatusCode,
            };
        }
        catch (ArgumentException e)
        {
            logger.LogInformation(e, "Invalid hikarinagi proxy path: {Path}", path);
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to proxy request to hikarinagi: {Path}", path);
            return StatusCode(StatusCodes.Status502BadGateway, e.ToString());
        }
    }
}
