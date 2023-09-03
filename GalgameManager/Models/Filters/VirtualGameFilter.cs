using GalgameManager.Contracts;

namespace GalgameManager.Models.Filters;

public class VirtualGameFilter : IFilter
{
    public bool Apply(Galgame galgame) => galgame.CheckExist();
}