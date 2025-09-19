using GalgameManager.Helpers;

namespace GalgameManager.Models.Sources;

/// <summary>
/// 虚拟游戏库，仅用于游戏库界面，游戏列表是<b>动态构造</b>的，不要依赖它
/// </summary>
public partial class VirtualSource : GalgameSourceBase
{
    public override GalgameSourceType SourceType => GalgameSourceType.Virtual;

    public override bool CanChangeScanOnStart => false;

    public override bool CanChangeCheckOnStart => false;

    public override bool CanChangeDetect => false;
    public override bool CanChangeSaveMetaBackup => false;

    public override bool IsGameAddable => false;
    public override bool IsSourceScanable => false;
    public override bool IsDelectable => false;
    public override bool ApplySearchKey(string searchKey) => Path.ContainX(searchKey);

    public VirtualSource()
    {
        CheckOnStart = false;
    }

    /// <summary>
    /// 根据allGames构造虚拟游戏游戏列表
    /// </summary>
    /// <param name="allGames"></param>
    public void UpdateGames(IList<Galgame> allGames)
    {
        HashSet<Galgame> allGamesSet = new(GetGalgameList());
        foreach (Galgame game in allGames)
        {
            if (game.IsLocalGame && allGamesSet.Contains(game)) DeleteGalgame(game);
            if (!game.IsLocalGame && !allGamesSet.Contains(game)) AddGalgame(game, string.Empty);
        }
    }
}