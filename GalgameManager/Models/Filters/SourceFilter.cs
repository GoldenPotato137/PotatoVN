using GalgameManager.Models.Sources;

namespace GalgameManager.Models.Filters;

public class SourceFilter : FilterBase
{
    private readonly GalgameSourceBase _source;

    public SourceFilter(GalgameSourceBase source)
    {
        Name = source.Name;
        SuggestName = $"{source.Name}/Source";
        _source = source;
    }

    public override bool Apply(Galgame galgame) => _source.Contain(galgame);

    public override string Name { get; }
    protected override string SuggestName { get; }
}