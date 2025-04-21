using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GalgameManager.Models;

public class SteamGame
{
    [JsonPropertyName("appid")]    
    public string _appid { get; set; } = string.Empty;
    [JsonPropertyName("name")]
    public string _name { get; set; } = string.Empty;



    // 游戏的总游玩时长
    [JsonPropertyName("playtime_forever")]
    public int _playtime_forever { get; set; }


    // 两周内的游玩时长
    [JsonPropertyName("playtime_2weeks")]
    public int _playtime_2weeks { get; set; } 

    [JsonPropertyName("rtime_last_played")]
    public int _rtime_last_played { get; set; } 


    //这些是游戏中各种图片的文件名。要构建图片的 URL，请使用以下格式：http://media.steampowered.com/steamcommunity/public/images/apps/ {appid} / {hash} .jpg。
    [JsonPropertyName("img_logo_url")]
    public string _img_icon_url { get; set; } = string.Empty;

    // 是否社区可见
    [JsonPropertyName("has_community_visible_stats")]
    public string _has_community_visible_stats { get; set; } = string.Empty;





}
