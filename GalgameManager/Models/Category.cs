using GalgameManager.Helpers;

namespace GalgameManager.Models;

public class Category
{
    private readonly List<Galgame> _galgames = new();
    public string Name = string.Empty;
    public readonly List<string> Galgames = new(); // 用于序列化, Path

    public bool Belong(Galgame galgame) => _galgames.Contains(galgame);

    public Category()
    {
        
    }

    public Category(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 更新序列化列表
    /// </summary>
    public void UpdateSerializeList()
    {
        Galgames.Clear();
        _galgames.ForEach(g => Galgames.Add(g.Path));
    }
    
    public void Add(Galgame galgame)
    {
        if (_galgames.Contains(galgame))
            throw new ArgumentException("EditableCategory_Add_AlreadyExist".GetLocalized());
        _galgames.Add(galgame);
    }
    
    public void Remove(Galgame galgame)
    {
        if (!_galgames.Contains(galgame)) return;
        _galgames.Remove(galgame);
    }
}