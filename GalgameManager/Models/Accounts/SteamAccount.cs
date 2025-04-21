using System.Text.Json.Serialization;

namespace GalgameManager.Models;
public class SteamAccount
{
    /*
    这里可以通过 
    http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/? key=XXXXXXXXXXXXXXXXXXXXXXX&steamids=76561197960435530
    来得到数据只需要我们知道用户的steamid就可以了， 这个key是我们要去steam上申请的
    申请地址是 https://steamcommunity.com/dev/apikey
    key = C6DA1657B8D37FF04F8D530D0C309B54
    下面是这个接口返回的数据
    */

    public enum CommunityVisibilityState
    {
        PRIVATE = 1,
        PUBLIC = 3,
    }

    public enum PersonaState
    {
        //离线
        OFFLINE = 0,

        //在线
        ONLINE = 1,
        //忙碌
        BUSY = 2,
        //离开
        AWAY = 3,
        //隐身
        SNOOZE = 4,
        //寻求交易
        LOOKING_TO_TRADE = 5,
        //寻求游戏
        LOOKING_TO_PLAY = 6,
    }

    // steamids
    [JsonPropertyName("steamid")]
    public required string _steamids { get; set; }

    //玩家的角色名称（显示名称）
    [JsonPropertyName("personaname")]
    public string? _personananme { get; set; }

    //玩家 Steam 社区个人资料的完整 URL。
    [JsonPropertyName("profileurl")]
    public string? _profileurl { get; set; }


    //玩家 32x32px 头像的完整 URL。如果用户尚未配置头像，则此 URL 将作为默认头像。
    [JsonPropertyName("avatar")]
    public string? _avatar { get; set; }


    //玩家 64x64px 头像的完整 URL。如果用户尚未配置头像，则此 URL 将作为默认头像。
    [JsonPropertyName("avatarmedium")]
    public string? _avatarmedium { get; set; }

    // 玩家 184x184px 头像的完整 URL。如果用户尚未配置头像，则此 URL 将作为默认头像。
    [JsonPropertyName("avatarfull")]
    public string? _avatarFull { get; set; }

    //用户的当前状态。
    [JsonPropertyName("personastate")]
    public PersonaState _personastate { get; set; }

    //这表示个人资料是否可见，如果可见，则说明允许您查看的原因。
    [JsonPropertyName("communityvisibilitystate")]
    public CommunityVisibilityState _communityvisibilitystate { get; set; }

    // 如果用户公开数据，在游玩时可以得到游戏的id
    [JsonPropertyName("gameid")]
    public string? _gameid { get; set; }

    // 如果用户公开数据，在游玩时可以得到游戏的名字
    [JsonPropertyName("gameextrainfo")]
    public string? _gameextrainfo { get; set; }

}
