using System.ComponentModel.DataAnnotations;
using GalgameManager.Server.Enums;
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace GalgameManager.Server.Models;

public class Galgame
{
    public const string DefaultString = "——";


    public int Id { get; set; }
    public User? User { get; set; }
    public required int UserId { get; set; }

    public long LastChangedTimeStamp { get; set; }
    
    #region GAME_SETTINGS
    public List<Category>? Categories { get; set; } = new();
    #endregion
    
    #region GAME_INFO
    [MaxLength(20)] public string? BgmId { get; set; }
    [MaxLength(20)] public string? VndbId { get; set; }
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string CnName { get; set; } = string.Empty;
    [MaxLength(2500)] public string Description { get; set; } = string.Empty;
    [MaxLength(200)] public string Developer { get; set; } = DefaultString;
    [MaxLength(200)] public string ExpectedPlayTime { get; set; } = DefaultString;
    public float Rating { get; set; }
    public long ReleaseDateTimeStamp { get; set; }
    [MaxLength(220)] public string? ImageLoc { get; set; }
    public List<string>? Tags { get; set; }
    #endregion

    #region PLAY_STATUS
    public List<PlayLog>? PlayTime { get; set; } = new();
    public int TotalPlayTime { get; set; } //单位：分钟
    public PlayType PlayType { get; set; } //游玩状态
    [MaxLength(1000)] public string Comment { get; set; } = string.Empty; //吐槽（评论）
    public int MyRate { get; set; } //我的评分
    public bool PrivateComment { get; set; } //是否私密评论
    #endregion
}