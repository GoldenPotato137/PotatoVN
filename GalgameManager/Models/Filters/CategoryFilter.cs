using GalgameManager.Helpers;

namespace GalgameManager.Models.Filters;

public class CategoryFilter : FilterBase
{
    private readonly Category _category;
    
    public CategoryFilter(Category category)
    {
        _category = category;
        Name = category.Name;
        SuggestName = $"{_category.Name}/{"Category".GetLocalized()}";
    }
    
    public override bool Apply(Galgame galgame)
    {
        return _category.Belong(galgame);
    }

    public override string Name { get; }

    protected override string SuggestName { get; }
}