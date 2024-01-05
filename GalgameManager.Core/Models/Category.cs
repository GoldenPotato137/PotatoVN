#nullable enable
namespace GalgameManager.Core.Models;

public class Category
{
    public int Id { get; set; }
    public List<Galgame>? Galgames { get; set; }
}