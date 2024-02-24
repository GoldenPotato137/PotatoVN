using System.ComponentModel.DataAnnotations;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GalgameManager.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class GalgameController (IGalgameService galService, IOssService ossService): ControllerBase
{
    /// <summary>获取galgame列表</summary>
    /// <remarks>获取最后一次更新时间严格晚于给定时间戳的galgame列表</remarks>
    /// <response code="400">pageIndex小于0或pageSize小于等于0</response>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<GalgameDto>>> GetGalgamesAsync([FromQuery][Required] long timestamp,
        [FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 10)
    {
        if (pageIndex < 0 || pageSize <= 0)
            return BadRequest("Invalid pageIndex or pageSize.");
        var userId = this.GetUserId();
        PagedResult<Galgame> tmp = await galService.GetGalgamesAsync(userId, timestamp, pageIndex, pageSize);
        PagedResult<GalgameDto> result = new(tmp.Items.ToDtoList(g => new GalgameDto(g)), tmp.PageIndex, 
            tmp.PageSize, tmp.Cnt);
        foreach(GalgameDto dto in result.Items)
            await dto.WithImgAsync(ossService, userId);
        return Ok(result);
    }

    /// <summary>获取已删除的galgame列表</summary>
    /// <remarks>获取删除时间严格晚于给定时间戳的galgame列表</remarks>
    [HttpGet("deleted")]
    [Authorize]
    public async Task<ActionResult<PagedResult<GalgameDeletedDto>>> GetGalgamesDeletedAsync(
        [FromQuery] [Required] long timestamp,
        [FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 10)
    {
        if (pageIndex < 0 || pageSize <= 0)
            return BadRequest("Invalid pageIndex or pageSize.");
        var userId = this.GetUserId();
        PagedResult<GalgameDeleted> tmp =
            await galService.GetDeletedGalgamesAsync(userId, timestamp, pageIndex, pageSize);
        PagedResult<GalgameDeletedDto> result = new(tmp.Items.ToDtoList(g => new GalgameDeletedDto(g)), 
            tmp.PageIndex, tmp.PageSize, tmp.Cnt);
        return Ok(result);
    }

    /// <summary>新建或更新galgame</summary>
    /// <remarks>
    /// 所有字段均可选，覆盖原字段 <br/>
    /// <b>若Id没有填，则认为是新建galgame</b> <br/>
    /// 务必注意字段是覆盖的，意味着游玩时间记录会覆盖掉原记录，若需要追加记录请使用[PATCH] /galgame/{id}/playlog <br/>
    /// </remarks>
    /// <response code="404">填入了id字段，但不存在具有该id的galgame</response>
    /// <response code="400">调用方不是该游戏所属者</response>
    [HttpPatch]
    [Authorize]
    public async Task<ActionResult<GalgameDto>> AddOrUpdateGalgameAsync([FromBody] GalgameUpdateDto payload)
    {
        var userId = this.GetUserId();
        try
        {
            Galgame galgame = await galService.AddOrUpdateGalgameAsync(userId, payload);
            return Ok(new GalgameDto(galgame));
        }
        catch (ArgumentException)
        {
            return NotFound("galgame with given id not found");
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest("You are not the owner of the galgame.");
        }
    }

    /// <summary>添加游玩记录或游玩时长</summary>
    /// <remarks>若已经有当天游戏的记录，则累加上去</remarks>
    /// <response code="404">游戏不存在</response>
    /// <response code="400">调用方不是该游戏所属者</response>
    [HttpPatch("{galgameId}/playlog")]
    [Authorize]
    public async Task<ActionResult<GalgameDto>> AddPlayLogAsync([FromRoute] [Required] int galgameId,
        [FromBody] PlayLogDto playLog)
    {
        var userId = this.GetUserId();
        try
        {
            Galgame? galgame = await galService.AddPlayLogAsync(userId, galgameId, playLog);
            return Ok(new GalgameDto(galgame!));
        }
        catch (ArgumentException)
        {
            return NotFound("galgame with given id not found");
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest("You are not the owner of the galgame.");
        }
    }

    /// <summary>删除游戏</summary>
    /// <remarks>只能删除自己的游戏</remarks>
    /// <response code="404">游戏不存在</response>
    /// <response code="400">该游戏不属于此用户</response>
    [HttpDelete("{galgameId}")]
    [Authorize]
    public async Task<ActionResult> DeleteGalgameAsync([FromRoute] [Required] int galgameId)
    {
        var userId = this.GetUserId();
        try
        {
            await galService.DeleteGalgameAsync(userId, galgameId);
            return Ok();
        }
        catch (ArgumentException)
        {
            return NotFound("galgame with given id not found");
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest("You are not the owner of the galgame.");
        }
    }
    
    /// <summary>删除用户的所有游戏</summary>
    [HttpDelete]
    [Authorize]
    public async Task<ActionResult> DeleteGalgamesAsync()
    {
        var userId = this.GetUserId();
        await galService.DeleteGalgamesAsync(userId);
        return Ok();
    }
}