namespace GalgameManager.Server.Models;

public class PlayLogDto
{
    public PlayLogDto() { } // For serialization

    public PlayLogDto(PlayLog playLog)
    {
        DateTimeStamp = playLog.DateTimeStamp;
        Minute = playLog.Minute;
    }

    /// <summary>
    /// 游玩日期当天0点0分的时间戳
    /// </summary>
    public long DateTimeStamp { get; set; }
    
    public int Minute { get; set; }
}