namespace GalgameManager.Server.Models;

public class PlayLog
{
    public Galgame? Galgame { get; set; }
    public int GalgameId { get; set; }
    public int Id { get; set; }
    
    public long DateTimeStamp { get; set; } // 游玩时间那一天的0点0秒的时间戳
    public int Minute { get; set; }
}