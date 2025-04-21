using GalgameManager.Models;
using Newtonsoft.Json;


namespace GalgameManager.Helpers.API;
public class AllSteamList
{
    [JsonProperty("game_count")]
    public int GameCount { get; set; }
    [JsonProperty("games")]
    public List<SteamGame>? Games { get; set; }
    
}