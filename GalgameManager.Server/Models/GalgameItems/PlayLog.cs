namespace GalgameManager.Server.Models;

public class PlayLog
{
    public Galgame? Galgame { get; set; }
    public int GalgameId { get; set; }
    public int Id { get; set; }
    
    public DateTime Date { get; set; } = DateTime.UnixEpoch;
    public int Minute { get; set; }
}