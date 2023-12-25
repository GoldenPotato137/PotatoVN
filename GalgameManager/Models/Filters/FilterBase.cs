using GalgameManager.Contracts;

namespace GalgameManager.Models.Filters;

public abstract class FilterBase : IFilter
{
    public abstract bool Apply(Galgame galgame);
    
    public abstract string Name { get; }

    /// <summary>
    /// 在添加过滤器时AutoSuggestBox会显示的内容
    /// </summary>
    protected abstract string SuggestName { get; }

    public override string ToString() => SuggestName;
}