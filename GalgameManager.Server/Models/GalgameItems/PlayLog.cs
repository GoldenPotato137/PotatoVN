namespace GalgameManager.Server.Models;

public class PlayLog
{
    public Galgame? Galgame { get; set; }
    public required int GalgameId { get; set; }
    public int Id { get; set; }
    
    public required long DateTimeStamp { get; set; } // 游玩时间那一天的0点0秒的时间戳
    public required int Minute { get; set; }
}