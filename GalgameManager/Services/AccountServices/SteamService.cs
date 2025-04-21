using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers.API;
using GalgameManager.Models;

namespace GalgameManager.Services.AccountServices;
public class SteamService : ISteamService
{
    private readonly string? key;
    private readonly string? steamId;
    private SteamAccount? steamAccount;
    private readonly ISteamApi _steamApi = new SteamApi();

    public Uri BaseUri => new("http://api.steampowered.com/"); 

    public SteamService(string? key, string? steamId)
    {
        this.key = key;
        this.steamId = steamId;
        
    }

    public SteamAccount? GetSteamAccountInfo(string steamid) // 
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(steamid))
        {
            return null;
        }

        var apiResponseTask = _steamApi.GetPlayerSummariesAsync(key, steamid);
        apiResponseTask.Wait(); 
        var apiResponse = apiResponseTask.Result; 

        if (apiResponse.Code == 200 && apiResponse.Data != null)
        {
            steamAccount = apiResponse.Data;
        }

        return steamAccount; 
    }

    public Dictionary<string, SteamGame> GetSteamGameList(string steamid) 
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(steamid))
        {
            return null;
        }
        var apiResponseTask = _steamApi.GetOwnedGamesAsync(key, steamid);
        apiResponseTask.Wait(); 
        var apiResponse = apiResponseTask.Result;
        Dictionary<string,SteamGame> result = new Dictionary<string,SteamGame>();
        if (apiResponse.Code==200&&apiResponse.Data.GameCount>0)
        {
            foreach(var game in apiResponse.Data.Games)
            {
                result[game._appid] = game;
            }
        }
        return result;

    }

    public void UpdateSteamGameInfo() 
    {

    }

    
}
