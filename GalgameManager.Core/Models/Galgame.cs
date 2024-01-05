#nullable enable
using GalgameManager.Core.Enums;
using Newtonsoft.Json;
// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace GalgameManager.Core.Models;

public class Galgame : IComparable<Galgame>
{
    public const string DefaultImagePath = "ms-appx:///Assets/WindowIcon.ico";
    public const string DefaultString = "——";
    public const string MetaPath = ".PotatoVN";


    public int Id { get; set; }
    
    #region GAME_SETTINGS
    public string Path { get; set; } = string.Empty;
    public string? SavePath { get; set; }   //云端存档本地路径
    public string? ExePath { get; set; }
    public string? ProcessName { get; set; } //手动指定的进程名，用于正确获取游戏进程
    public bool RunAsAdmin { get; set; }    //是否以管理员权限运行
    [JsonIgnore] public List<Category>? Categories { get; set; } = new();
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
    public string ImagePath { get; set; } = DefaultImagePath;
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
    
    [JsonIgnore] public static SortKeys[] SortKeysList
    {
        get;
        private set;
    } = { SortKeys.LastPlay , SortKeys.Developer};

    [JsonIgnore] public static bool[] SortKeysAscending
    {
        get;
        private set;
    } = {false, false};

    public Galgame()
    {
    }

    /// <summary>
    /// 更新CompareTo参数，可用于Sort
    /// sortKeysList 和 sortKeysAscending长度相同
    /// </summary>
    /// <param name="sortKeysList"></param>
    /// <param name="sortKeysAscending">升序/降序: true/false</param>
    public static void UpdateSortKeys(SortKeys[] sortKeysList, bool[] sortKeysAscending)
    {
        SortKeysList = sortKeysList;
        SortKeysAscending = sortKeysAscending;
    }
    
    public int CompareTo(Galgame? b)
    {
        if (b is null ) return 1;
        for (var i = 0; i < Math.Min(SortKeysList.Length, SortKeysAscending.Length); i++)
        {
            var result = 0;
            var take = SortKeysAscending[i]?-1:1; //true升序, false降序
            switch (SortKeysList[i])
            {
                case SortKeys.Developer:
                    result = string.Compare(Developer, b.Developer, StringComparison.Ordinal);
                    break;
                case SortKeys.Name:
                    result = string.Compare(Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                    take *= -1;
                    break;
                case SortKeys.Rating:
                    result = Rating.CompareTo(b.Rating);
                    break;
                case SortKeys.LastPlay:
                    result = LastPlay.CompareTo(b.LastPlay);
                    break;
                case SortKeys.ReleaseDate:
                    result = ReleaseDate.CompareTo(b.ReleaseDate);
                    break;
            }
            if (result != 0)
                return take * result; 
        }
        return 0;
    }
}


public enum SortKeys
{
    Name,
    LastPlay,
    Developer,
    Rating,
    ReleaseDate
}
