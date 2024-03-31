using GalgameManager.Contracts.Models;
using GalgameManager.Helpers;

namespace GalgameManager.Models.Filters;

public class VirtualGameFilter : FilterBase
{
    public override bool Apply(Galgame galgame) => galgame.SourceType != GalgameSourceType.Virtual;
    
    public override string Name { get; } = "VirtualGameFilter".GetLocalized();

    protected override string SuggestName { get; } = "VirtualGameFilter".GetLocalized();
}