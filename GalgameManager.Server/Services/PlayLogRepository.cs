using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Server.Services;

public class PlayLogRepository (DataContext context): IPlayLogRepository
{
    public async Task<PlayLog?> GetPlayLogAsync(int galgameId, long dateTimeStamp)
    {
        return await context.GalPlayLog
            .Where(log => log.GalgameId == galgameId && log.DateTimeStamp == dateTimeStamp)
            .FirstOrDefaultAsync();
    }

    public async Task<PlayLog> AddOrUpdatePlayLogAsync(PlayLog playLog)
    {
        context.GalPlayLog.Update(playLog);
        await context.SaveChangesAsync();
        return playLog;
    }

    public async Task SetPlayLogsAsync(int galgameId, List<PlayLog> playLogs)
    {
        List<PlayLog> logs = await context.GalPlayLog.Where(log => log.GalgameId == galgameId).ToListAsync();
        Dictionary<long, PlayLog> newLogs = new();
        foreach (PlayLog log in playLogs)
            newLogs[log.DateTimeStamp] = log;
        HashSet<long> existingTimestamp = [];
        foreach (PlayLog log in logs)
        {
            if (newLogs.TryGetValue(log.DateTimeStamp, out PlayLog? newLog))
            {
                log.Minute = newLog.Minute;
                context.GalPlayLog.Update(log);
                existingTimestamp.Add(log.DateTimeStamp);
            }
            else
                context.GalPlayLog.Remove(log);
        }
        foreach (PlayLog log in playLogs.Where(log => existingTimestamp.Contains(log.DateTimeStamp) == false)) 
            context.GalPlayLog.Add(log);
        await context.SaveChangesAsync();
    }

    public async Task DeletePlayLogsAsync(int galgameId)
    {
        IQueryable<PlayLog> query = context.GalPlayLog.Where(log => log.GalgameId == galgameId);
        await query.ExecuteDeleteAsync();
    }
}