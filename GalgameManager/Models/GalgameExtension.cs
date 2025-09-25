using GalgameManager.Helpers;

namespace GalgameManager.Models;

public static class GalgameExtension
{
    /// <summary>
    /// 试图从游戏根目录中找到存档位置（仅能找到已同步到服务器的存档）
    /// </summary>
    public static void FindSaveInPath(this Galgame game)
    {
        if (!game.CheckExistLocal() || game.LocalPath is not { } path) return;
        try
        {
            var cnt = 0;
            string? result = null;
            foreach (var subDir in Directory.GetDirectories(path))
                if (FolderOperations.IsSymbolicLink(subDir))
                {
                    cnt++;
                    result = subDir;
                }
            if (cnt == 1)
                game.SavePath = result;
        }
        catch (Exception e)
        {
            game.ErrorOccurred?.Invoke(e);
        }
    }
    
    public static bool ApplySearchKey(this Galgame game, string searchKey)
    {
        return game.Name.Value!.ContainX(searchKey) || 
               game.ChineseName.Value!.ContainX(searchKey) ||
               game.OriginalName.Value!.ContainX(searchKey) ||
               game.Developer.Value!.ContainX(searchKey) || 
               game.Tags.Value!.Any(str => str.ContainX(searchKey));
    }
}