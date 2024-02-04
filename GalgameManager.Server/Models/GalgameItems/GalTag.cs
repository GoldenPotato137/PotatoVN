namespace GalgameManager.Server.Models;

public class GalTag
{
    public Galgame? Galgame { get; set; }
    public int GalgameId { get; set; }
    public int Id { get; set; }

    public string Tag { get; set; } = string.Empty;
}