using System.Net;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Models;
using GalgameManager.Server.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class OssController(IOssService ossService, ILogger<OssController> logger, IUserRepository userRepository)
    : ControllerBase
{
    /// <summary>获取oss预签名上传路径</summary>
    /// <param name="objectFullName">上传文件名（包括前缀），如：Galgame/114514.jpg</param>
    /// <response code="400">objectFullName不合法 或 用户存储容量已满</response>
    [HttpGet("put")]
    [Authorize]
    public async Task<ActionResult<string>> GetPutPresignedUrl(string objectFullName = "")
    {
        User? user = await userRepository.GetUserAsync(this.GetUserId());
        if (user is null) return BadRequest("User not found");
        if (user.UsedSpace >= user.TotalSpace) return BadRequest("User used space exceed max space");
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

    /// <summary>Minio桶事件端口</summary>
    /// <remarks>需要在Minio事件WebHook中设置和环境变量AppSettings:Minio:EventToken一样的token</remarks>
    /// <response code="401">Token不正确</response>
    [HttpPost("event")]
    public async Task<ActionResult> MinioEvent([FromBody] MinioEvent minioEvent)
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized();
        var token = authHeader.Substring("Bearer ".Length).Trim();
        if (token != ossService.OssEventToken)
            return Unauthorized();

        foreach (Record record in minioEvent.Records)
        {
            record.S3.Bucket.Name = WebUtility.UrlDecode(record.S3.Bucket.Name);
            record.S3.Object.Key = WebUtility.UrlDecode(record.S3.Object.Key);
            if (record.S3.Bucket.Name != ossService.BucketName) continue;
            logger.LogInformation("Update user used space, Key: {Key}", record.S3.Object.Key);
            await ossService.UpdateUserUsedSpaceAsync(record.S3.Object);
        }

        await Task.CompletedTask;
        return Ok();
    }
}