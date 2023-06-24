using GalgameManager.Contracts;

namespace GalgameManager.Models;

public class CategoryGroup
{
    public string Name = string.Empty;
    public List<ICategory> Categories = new();

    public CategoryGroup()
    {
    }

    public CategoryGroup(string name)
    {
        Name = name;
    }
}