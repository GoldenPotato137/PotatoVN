using System;
using System.Collections.Generic;
using System.Linq;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using LiteDB;

namespace GalgameManager.Models;

public class CategoryGroup
{
    [BsonId] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    [BsonIgnore] public List<Category> Categories { get; set; } = new();
    public CategoryGroupType Type { get; set; }

    #region LITEDB_MAPPING

    public List<Guid> CategoryIds
    {
        get => Categories.Select(c => c.Id).ToList();
        set => _categoryIds = value;
    }

    public List<Guid> GetLoadedCategoryIds() => _categoryIds;
    private List<Guid> _categoryIds = new();

    #endregion

    public CategoryGroup()
    {
    }

    public CategoryGroup(string name, CategoryGroupType type)
    {
        Type = type;
        Name = name;
    }

    public override string ToString() => Name;

    public CategoryGroup Clone()
    {
        CategoryGroup result = (CategoryGroup)MemberwiseClone();
        result.Categories = Categories.Select(c => c.DeepClone()).ToList();
        return result;
    }

    public int GamesCount => Categories.Sum(c => c.GalgamesX.Count);
}
