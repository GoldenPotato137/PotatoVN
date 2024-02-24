using GalgameManager.Server.Contracts;
using GalgameManager.Server.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class OssController(IOssService ossService) : ControllerBase
{
    /// <summary>获取oss预签名上传路径</summary>
    /// <param name="objectFullName">上传文件名（包括前缀），如：Galgame/114514.jpg</param>
    /// <response code="400">objectFullName不合法</response>
    [HttpGet("put")]
    [Authorize]
    public async Task<ActionResult<string>> GetPutPresignedUrl(string objectFullName = "")
    {
        var result = await ossService.GetWritePresignedUrlAsync(this.GetUserId(), objectFullName);
        if (result is null)
            return BadRequest("Invalid object name");
        return Ok(result);
    }

    /// <summary>获取oss预签名读取路径</summary>
    /// <param name="objectFullName">上传文件名（包括前缀），如：Galgame/114514.jpg</param>
    /// <response code="404">文件不存在</response>
    [HttpGet("get")]
    [Authorize]
    public async Task<ActionResult<string>> GetGetPresignedUrl(string objectFullName = "")
    {
        var result = await ossService.GetReadPresignedUrlAsync(this.GetUserId(), objectFullName);
        if (result is null)
            return NotFound("Object not found");
        return Ok(result);
    }
}