using Windows.UI.StartScreen;

using GalgameManager.Contracts.Services;
using GalgameManager.Models;

namespace GalgameManager.Services;

/// <summary>
/// JumpList 管理
/// "/j galgamePath"
/// </summary>
public class JumpListService : IJumpListService
{
    private JumpList? _jumpList;
    private const int MaxItems = 5;

    private async Task Init()
    {
        _jumpList = await JumpList.LoadCurrentAsync();
        _jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
    }

    public async Task CheckJumpListAsync(List<Galgame> galgames)
    {
        if (_jumpList == null) await Init();
        List<JumpListItem> toRemove = _jumpList!.Items.Where(item => galgames.All(gal => $"/j \"{gal.Path}\"" != item.Arguments)).ToList();
        foreach (JumpListItem item in toRemove)
        {
            _jumpList.Items.Remove(item);
        }
        await _jumpList!.SaveAsync();
    }

    public async Task AddToJumpListAsync(Galgame galgame)
    {
        if (_jumpList == null) await Init();
        IList<JumpListItem>? items = _jumpList!.Items;
        JumpListItem? item = items.FirstOrDefault(i => i.Arguments == $"/j \"{galgame.Path}\"");
        if (item == null)
        {
            item = JumpListItem.CreateWithArguments($"/j \"{galgame.Path}\"", galgame.Name);
            item.Logo = new Uri("ms-appx:///Assets/heart.png");
        }
        else
            items.Remove(item); 
        items.Insert(0, item);
        if (items.Count > MaxItems)
            items.RemoveAt(items.Count-1);
        await _jumpList!.SaveAsync();
    }

    public async Task RemoveFromJumpListAsync(Galgame galgame)
    {
        if (_jumpList == null) await Init();
        IList<JumpListItem>? items = _jumpList!.Items;
        JumpListItem? item = items.FirstOrDefault(i => i.Arguments == $"/j \"{galgame.Path}\"");
        if (item != null)
        {
            items.Remove(item);
            await _jumpList!.SaveAsync();
        }
    }
}
