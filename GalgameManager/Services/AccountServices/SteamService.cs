using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers.API.Steam;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Services;

public class SteamService(IGalgameCollectionService gameService) : ISteamService
{
    private const string Key = "";
    private string? SteamId { get; set; }
    private readonly ISteamApi _steamApi = SteamAPi.GetApi();

    public async Task InitAsync(string steamid)
    {
        var account = await _steamApi.GetPlayerSummariesAsync(Key, steamid);
        if (account.response?.players?.Count == 0)
        {
            throw new PvnException("请检查steamID是否正确");
        }
        SteamId = steamid;
    }

    public async Task<SteamAccountDto> GetSteamAccountAsync()
    {
        if (SteamId is null) throw new PvnException("请先初始化SteamService");
        var account = await _steamApi.GetPlayerSummariesAsync(Key, SteamId);
        return account.response.players[0]; //这里有可能会有多个玩家但是先不考虑吧
    }

    public async Task<List<SteamGameDto>> GetSteamGameListResponseAsync()
    {
        if (SteamId is null) throw new PvnException("请先初始化SteamService");
        SteamResponseDto<SteamGameListResponseDto> gameList = await _steamApi.GetOwnedGamesAsync(Key, SteamId);
        return gameList.response.games;
    }

    public async Task<List<SteamGameDto>> GetGalgameListResponseAsync()
    {
        var gameList = await GetSteamGameListResponseAsync();
        List<SteamGameDto> galgameList = [];
        foreach (var game in gameList)
        {
            // 这里只需判断这个steam游戏是不是galgame，即vndb里有没有同名条目，
            // 不要求它在映射库里有steam映射（TryGetSteamIdAsync只认完全匹配的steam映射，会漏掉不少galgame）
            if (await PhraseHelper.TryGetVndbIdAsync(game.name) != null)
            {
                galgameList.Add(game);
            }
        }
        return galgameList;
    }

    public async Task UpdateSteamGalGameAsync(List<SteamGameDto> list, Galgame galgame)
    {
        foreach (var game in list)
        {
            if (game.appid == galgame.GetId(RssType.Steam))
            {
                galgame.TotalPlayTime = game.playtime_forever;
                galgame.LastPlayTime = DateTimeOffset.FromUnixTimeSeconds(game.rtime_last_played).LocalDateTime;
                await gameService.SaveGalgameAsync(galgame);
                break;
            }

        }
    }
}
