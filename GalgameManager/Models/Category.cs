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

    public string DisplayCount()
    {
        return "×" + _galgames.Count;
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
        galgame.Categories.Add(this);
    }

    public void Add(Category category)
    {
        if (category == this) return;
        category._galgames.ForEach(Add);
    }
    
    public void Remove(Galgame galgame)
    {
        if (!_galgames.Contains(galgame)) return;
        _galgames.Remove(galgame);
        galgame.Categories.Remove(this);
    }

    public void Delete()
    {
        _galgames.ForEach(g => g.Categories.Remove(this));
    }

    public override string ToString()
    {
        return Name;
    }
}