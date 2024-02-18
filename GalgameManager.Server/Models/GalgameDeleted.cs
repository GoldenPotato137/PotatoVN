namespace GalgameManager.Server.Models;

public class GalgameDeleted
{
    public int Id { get; set; }
    public User? User { get; set; }
    public required int UserId { get; set; }
    public int GalgameId { get; set; }
    public long DeleteTimeStamp { get; set; }
}