using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Contracts.Services;

public interface IGalgameSourceService
{
    /// <summary>
    /// 将游戏移入某个库，应该直接返回一个BgTaskBase（SourceMoveTaskBase）实例
    /// </summary>
    /// <param name="target">目标库</param>
    /// <param name="game">游戏</param>
    /// <param name="targetPath">目标路径，若为null则表示服务可自行决定路径</param>
    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null);
    
    /// <summary>
    /// 将游戏移出某个库，应该直接返回一个BgTaskBase（SourceMoveTaskBase）实例
    /// </summary>
    /// <param name="target">目标库</param>
    /// <param name="game">游戏</param>
    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game);

    /// <summary>
    /// 在库中保存游戏的Meta
    /// </summary>
    public Task SaveMetaAsync(Galgame game);

    /// <summary>
    /// 从游戏文件夹游戏Meta，若不存在则返回null
    /// </summary>
    /// <param name="path">文件夹路径</param>
    /// <returns></returns>
    public Task<Galgame?> LoadMetaAsync(string path);

    /// <summary>
    /// 在转换游戏位置时，给ChangeSourceDialog提供附加设置控件，返回null则不附加任何控件
    /// </summary>
    /// <param name="source"></param>
    /// <param name="setting"></param>
    /// <returns></returns>
    public Task<Grid?> GetAdditionSettingControlAsync(GalgameSourceBase source, ChangeSourceDialogAttachSetting setting);
    
    /// <summary>
    /// 获取库的（总空间，已用空间）（byte），若无法获取则返回(-1,-1)
    /// </summary>
    /// <param name="source"></param>
    public Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source);

    /// <summary>
    /// 获取移入描述
    /// </summary>
    /// <param name="target"></param>
    /// <param name="targetPath"></param>
    /// <returns></returns>
    public string GetMoveInDescription(GalgameSourceBase target, string targetPath);

    /// <summary>
    /// 获取将游戏移出某个库的描述
    /// </summary>
    /// <param name="target"></param>
    /// <param name="galgame"></param>
    /// <returns></returns>
    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame);
}