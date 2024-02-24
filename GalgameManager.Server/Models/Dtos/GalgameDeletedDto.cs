namespace GalgameManager.Server.Models;

public class GalgameDeletedDto (GalgameDeleted galgameDeleted)
{
    public int GalgameId { get; set; } = galgameDeleted.GalgameId;
    public long DeleteTimeStamp { get; set; } = galgameDeleted.DeleteTimeStamp;
}