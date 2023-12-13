using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Models;

public partial class Category : ObservableObject
{
    private readonly List<Galgame> _galgames = new();
    public string Name = string.Empty;
    public readonly List<string> Galgames = new(); // 用于序列化, Path
    [ObservableProperty] private string _imagePath = string.Empty;

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
        foreach (Galgame game in _galgames)
            Galgames.Add(game.CheckExist()? game.Path : game.Name!);
    }
    
    public void Add(Galgame galgame)
    {
        if (_galgames.Contains(galgame)) return;
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