namespace GalgameManager.Server.Models;

public class Category
{
    public int Id { get; set; }
    public List<Galgame>? Galgames { get; set; }
}