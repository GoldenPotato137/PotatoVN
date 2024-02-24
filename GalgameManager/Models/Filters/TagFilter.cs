using System.Collections.ObjectModel;

namespace GalgameManager.Models.Filters;

public class TagFilter : FilterBase
{
    private readonly string _tag;

    public TagFilter(string tag)
    {
        _tag = tag;
        Name = tag;
        SuggestName = $"{_tag}/Tag";
    }
    
    public override bool Apply(Galgame galgame)
    {
        return (galgame.Tags.Value ?? new ObservableCollection<string>()).Contains(_tag);
    }

    public override string Name { get; }

    protected override string SuggestName { get; }
}