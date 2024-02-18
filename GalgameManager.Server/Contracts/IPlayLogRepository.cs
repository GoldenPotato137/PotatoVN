using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IPlayLogRepository
{
    public Task<PlayLog?> GetPlayLogAsync(int galgameId, long dateTimeStamp);
    
    public Task<PlayLog> AddOrUpdatePlayLogAsync(PlayLog playLog);

    public Task SetPlayLogsAsync(int galgameId, List<PlayLog> playLogs);
    
    public Task DeletePlayLogsAsync(int galgameId);
}