using GalgameManager.Server.Enums;

namespace GalgameManager.Server.Models;

public class Galgame
{
    public const string DefaultString = "——";


    public int Id { get; set; }
    public int UserId { get; set; }
    
    #region GAME_SETTINGS
    public List<Category>? Categories { get; set; } = new();
    #endregion
    
    #region GAME_INFO
    public string? BgmId { get; set; }
    public string? VndbId { get; set; }
    public string? MixedId { get; set; }
    public RssType RssType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CnName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Developer { get; set; } = DefaultString;
    public string ExpectedPlayTime { get; set; } = DefaultString;
    public float Rating { get; set; }
    public DateTime ReleaseDate { get; set; } = DateTime.MinValue;
    public string? ImageUrl { get; set; }
    public List<GalTag>? Tags { get; set; } = new();
    #endregion

    #region PLAY_STATUS
    public List<PlayLog>? PlayTime { get; set; } = new();
    public DateTime LastPlay { get; set; } = DateTime.MinValue;
    public int TotalPlayTime { get; set; } //单位：分钟
    public PlayType PlayType { get; set; } //游玩状态
    public string Comment { get; set; } = string.Empty; //吐槽（评论）
    public int MyRate { get; set; } //我的评分
    public bool PrivateComment { get; set; } //是否私密评论
    #endregion
}