using GalgameManager.Core.Helpers;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;

namespace GalgameManager.Server.Services;

public class GalgameService(IGalgameRepository galRep, IGalgameDeletedRepository galDeletedRep, 
    IPlayLogRepository playLogRep, IUserService userService, IOssService ossService)
    : IGalgameService
{
    public async Task<Galgame?> GetGalgameAsync(int id)
    {
        return await galRep.GetGalgameAsync(id);
    }

    public async Task<PagedResult<Galgame>> GetGalgamesAsync(int userId, long timestamp, int pageIndex, int pageSize)
    {
        return await galRep.GetGalgamesAsync(userId, timestamp, pageIndex, pageSize);
    }

    public async Task<Galgame> AddOrUpdateGalgameAsync(int userId, GalgameUpdateDto payload)
    {
        Galgame? galgame;
        if (payload.Id != null)
        {
            galgame = await galRep.GetGalgameAsync(payload.Id ?? 0);
            if (galgame is null)
                throw new ArgumentException("Galgame not found.");
            if(galgame.UserId != userId)
                throw new UnauthorizedAccessException("You are not the owner of this galgame.");
        }
        else
        {
            galgame = new Galgame
            {
                UserId = userId,
            };
            await galRep.AddGalgameAsync(galgame);
        }

        galgame.BgmId = payload.BgmId ?? galgame.BgmId;
        galgame.VndbId = payload.VndbId ?? galgame.VndbId;
        galgame.Name = payload.Name ?? galgame.Name;
        galgame.CnName = payload.CnName ?? galgame.CnName;
        galgame.Description = payload.Description ?? galgame.Description;
        galgame.Developer = payload.Developer ?? galgame.Developer;
        galgame.ExpectedPlayTime = payload.ExpectedPlayTime ?? galgame.ExpectedPlayTime;
        galgame.Rating = payload.Rating ?? galgame.Rating;
        galgame.ReleaseDateTimeStamp = payload.ReleaseDateTimeStamp ?? galgame.ReleaseDateTimeStamp;
        galgame.ImageLoc = payload.ImageLoc ?? galgame.ImageLoc;
        galgame.Tags = payload.Tags ?? galgame.Tags;

        if (payload.PlayTime is not null)
        {
            List<PlayLog> newLogs = [];
            foreach (PlayLogDto playLogDto in payload.PlayTime)
                newLogs.Add(new PlayLog
                {
                    GalgameId = galgame.Id,
                    DateTimeStamp = playLogDto.DateTimeStamp,
                    Minute = playLogDto.Minute
                });
            await playLogRep.SetPlayLogsAsync(galgame.Id, newLogs);
        }

        galgame.TotalPlayTime = payload.TotalPlayTime ?? galgame.TotalPlayTime;
        galgame.PlayType = payload.PlayType ?? galgame.PlayType;
        galgame.Comment = payload.Comment ?? galgame.Comment;
        galgame.MyRate = payload.MyRate ?? galgame.MyRate;
        galgame.PrivateComment = payload.PrivateComment ?? galgame.PrivateComment;

        galgame.LastChangedTimeStamp = DateTime.Now.ToUnixTime();
        await galRep.AddOrUpdateGalgameAsync(galgame);
        await userService.UpdateLastModifiedAsync(userId, galgame.LastChangedTimeStamp);

        return galgame;
    }

    public async Task<Galgame?> AddPlayLogAsync(int userId, int galgameId, PlayLogDto payload)
    {
        Galgame? galgame = await galRep.GetGalgameAsync(galgameId, true);
        if (galgame is null)
            throw new ArgumentException("Galgame not found.");
        if (galgame.UserId != userId)
            throw new UnauthorizedAccessException("You are not the owner of this galgame.");
        PlayLog? log = await playLogRep.GetPlayLogAsync(galgameId, payload.DateTimeStamp);
        if (log is not null)
            log.Minute += payload.Minute;
        else
        {
            log = new()
            {
                GalgameId = galgameId,
                DateTimeStamp = payload.DateTimeStamp,
                Minute = payload.Minute
            };
        }

        await playLogRep.AddOrUpdatePlayLogAsync(log);
        galgame.TotalPlayTime += payload.Minute;
        galgame.LastChangedTimeStamp = DateTime.Now.ToUnixTime();
        await galRep.AddOrUpdateGalgameAsync(galgame);
        await userService.UpdateLastModifiedAsync(userId, galgame.LastChangedTimeStamp);
        
        return galgame;
    }
    
    public async Task DeleteGalgameAsync(int userId, int id)
    {
        Galgame? gal = await galRep.GetGalgameAsync(id);
        if (gal is null) throw new ArgumentException("Galgame not found.");
        if (gal.UserId != userId) throw new UnauthorizedAccessException("You are not the owner of this galgame.");
        var timestamp = DateTime.Now.ToUnixTime();
        await galRep.DeleteGalgameAsync(id);
        await galDeletedRep.AddGalgameDeletedAsync(new GalgameDeleted
        {
            DeleteTimeStamp = timestamp,
            GalgameId = id,
            UserId = gal.UserId
        });
        await userService.UpdateLastModifiedAsync(gal.UserId, timestamp);
        if(gal.ImageLoc is not null) await ossService.DeleteObjectAsync(userId, gal.ImageLoc);
    }

    public async Task DeleteGalgamesAsync(int userId)
    {
        var timestamp = DateTime.Now.ToUnixTime();
        PagedResult<Galgame> gals = await galRep.GetGalgamesAsync(userId, 0, 0, 1000000);
        foreach (Galgame game in gals.Items.Where(g => g.ImageLoc is not null))
            await ossService.DeleteObjectAsync(userId, game.ImageLoc!);
        List<int> ids = await galRep.DeleteGalgamesAsync(userId);
        foreach (var id in ids)
            await galDeletedRep.AddGalgameDeletedAsync(new GalgameDeleted
            {
                DeleteTimeStamp = timestamp,
                GalgameId = id,
                UserId = userId
            });
        await userService.UpdateLastModifiedAsync(userId, timestamp);
    }

    public async Task<PagedResult<GalgameDeleted>> GetDeletedGalgamesAsync(int userId, long timestamp, int pageIndex,
        int pageSize)
    {
        if(pageIndex < 0 || pageSize < 0)
            throw new ArgumentException("Invalid pageIndex or pageSize.");
        return await galDeletedRep.GetGalgameDeletedsAsync(userId, timestamp, pageIndex, pageSize);
    }
}