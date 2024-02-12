namespace GalgameManager.Server.Models;

public class PlayLogDto (PlayLog playLog)
{
    public long DateTimeStamp { get; set; } = playLog.DateTimeStamp;
    public int Minute { get; set; } = playLog.Minute;
}