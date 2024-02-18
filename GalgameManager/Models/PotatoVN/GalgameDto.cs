using GalgameManager.Enums;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming

namespace GalgameManager.Models;

public class GalgameDto
{
    public int id { get; set; }
    public string? bgmId { get; set; }
    public string? vndbId { get; set; }
    public string? name { get; set; }
    public string? cnName { get; set; }
    public string? description { get; set; }
    public string? developer { get; set; }
    public string? expectedPlayTime { get; set; }
    public float rating { get; set; }
    public long? releasedDateTimeStamp { get; set; }
    public string? imageUrl { get; set; }
    public List<string>? tags { get; set; }
    public List<PlayLogDto>? playTime { get; set; }
    public int totalPlayTime { get; set; }
    public PlayType playType { get; set; }
    public string? comment { get; set; }
    public int myRate { get; set; }
    public bool privateComment { get; set; }
}