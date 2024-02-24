using GalgameManager.Server.Contracts;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Enums;

namespace GalgameManager.Server.Models;

public class GalgameDto(Galgame galgame)
{
    public int Id { get; set; } = galgame.Id;

    #region GAMEINFO

    public string? BgmId { get; set; } = galgame.BgmId;
    public string? VndbId { get; set; } = galgame.VndbId;
    public string Name { get; set; } = galgame.Name;
    public string CnName { get; set; } = galgame.CnName;
    public string Description { get; set; } = galgame.Description;
    public string Developer { get; set; } = galgame.Developer;
    public string ExpectedPlayTime { get; set; } = galgame.ExpectedPlayTime;
    public float Rating { get; set; } = galgame.Rating;
    public long ReleasedDateTimeStamp { get; set; } = galgame.ReleaseDateTimeStamp;
    public string? ImageUrl { get; set; }
    public List<string>? Tags { get; set; } = galgame.Tags;

    #endregion

    #region PLAY_STATUS

    public List<PlayLogDto>? PlayTime { get; set; } = galgame.PlayTime?.ToDtoList(l => new PlayLogDto(l));
    public int TotalPlayTime { get; set; } = galgame.TotalPlayTime;
    public PlayType PlayType { get; set; } = galgame.PlayType;
    public string Comment { get; set; } = galgame.Comment;
    public int MyRate { get; set; } = galgame.MyRate;
    public bool PrivateComment { get; set; } = galgame.PrivateComment;

    #endregion

    public async Task<GalgameDto> WithImgAsync(IOssService ossService, int userId)
    {
        ImageUrl = await ossService.GetReadPresignedUrlAsync(userId, galgame.ImageLoc ?? string.Empty);
        return this;
    }
}

public class GalgameUpdateDto
{
    public int? Id { get; set; }
    public string? BgmId { get; set; }
    public string? VndbId { get; set; }
    public string? Name { get; set; }
    public string? CnName { get; set; }
    public string? Description { get; set; }
    public string? Developer { get; set; }
    public string? ExpectedPlayTime { get; set; }
    public float? Rating { get; set; }
    public long? ReleaseDateTimeStamp { get; set; }
    public string? ImageLoc { get; set; }
    public List<string>? Tags { get; set; }
    public int? TotalPlayTime { get; set; }
    public PlayType? PlayType { get; set; }
    public List<PlayLogDto>? PlayTime { get; set; }
    public string? Comment { get; set; }
    public int? MyRate { get; set; }
    public bool? PrivateComment { get; set; }
}