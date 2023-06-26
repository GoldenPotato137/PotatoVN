using GalgameManager.Contracts;
using GalgameManager.Enums;

namespace GalgameManager.Models;

public class CategoryGroup
{
    public string Name = string.Empty;
    public List<Category> Categories = new();
    public readonly CategoryGroupType Type;

    public CategoryGroup()
    {
    }

    public CategoryGroup(string name, CategoryGroupType type)
    {
        Type = type;
        Name = name;
    }

    public CategoryGroup(string name)
    {
        Name = name;
    }
}