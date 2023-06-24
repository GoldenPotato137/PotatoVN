using GalgameManager.Contracts;
using GalgameManager.Helpers;

namespace GalgameManager.Models;

public class EditableCategory : ICategory
{
    private readonly List<Galgame> _galgames = new();
    private readonly string _name = string.Empty;

    public bool Belong(Galgame galgame) => _galgames.Contains(galgame);
    public string Name() => _name;

    public void Add(Galgame galgame)
    {
        if (_galgames.Contains(galgame))
            throw new ArgumentException("EditableCategory_Add_AlreadyExist".GetLocalized());
        _galgames.Add(galgame);
    }
    
    public void Remove(Galgame galgame)
    {
        if (!_galgames.Contains(galgame))
            throw new ArgumentException("EditableCategory_Remove_NotExist".GetLocalized());
        _galgames.Remove(galgame);
    }
}