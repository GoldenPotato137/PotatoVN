using GalgameManager.Contracts;

namespace GalgameManager.Models.Filters;

public class CategoryFilter : IFilter
{
    private readonly Category _category;
    
    public CategoryFilter(Category category)
    {
        _category = category;
    }
    
    public bool Apply(Galgame galgame)
    {
        return _category.Belong(galgame);
    }
}