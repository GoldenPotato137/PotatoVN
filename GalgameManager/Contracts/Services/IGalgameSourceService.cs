using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;

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
    /// 在库中保存游戏的Meta，内部应自行处理不需要保存Meta的源
    /// </summary>
    /// <param name="game">要保存的游戏</param>
    /// <param name="targetSource">指定要保存哪个库，如果不填，则保存所有该service能负责的库</param>
    public Task SaveMetaAsync(Galgame game, GalgameSourceBase? targetSource = null);

    /// <summary>
    /// 从游戏文件夹游戏Meta，若不存在则返回null
    /// </summary>
    /// <param name="path">文件夹路径</param>
    /// <exception cref="PvnException">当.PotatoVN存在但meta.json不存在时抛出</exception>
    /// <returns></returns>
    public Task<Galgame?> LoadMetaAsync(string path);
    
    /// <summary>
    /// 从库中删除游戏的Meta文件夹
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public Task RemoveMetaAsync(Galgame game);
    
    /// <summary>
    /// 获取库的（总空间，已用空间）（byte），若无法获取则返回(-1,-1)
    /// </summary>
    /// <param name="source"></param>
    public Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source);
    
    /// <summary>
    /// 监听库的变化
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public Task AddListenAsync(GalgameSourceBase source);
    
    /// <summary>
    /// 取消监听库的变化
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public Task RemoveListenAsync(GalgameSourceBase source);

    /// <summary>
    /// 获取移入描述 <br/>
    /// 该方法只会在移动游戏对话框中被调用
    /// </summary>
    /// <param name="target"></param>
    /// <param name="targetPath"></param>
    /// <returns></returns>
    public string GetMoveInDescription(GalgameSourceBase target, string targetPath);

    /// <summary>
    /// 获取将游戏移出某个库的描述 <br/>
    /// 该方法只会在移动游戏对话框中被调用
    /// </summary>
    /// <param name="target"></param>
    /// <param name="galgame"></param>
    /// <returns></returns>
    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame);

    /// <summary>
    /// 从游戏路径获取其源应该在的路径
    /// </summary>
    /// <returns></returns>
    public Task<string> GetSourcePathAsync(string gamePath);

    /// <summary>
    /// 检查移动操作是否合法，若合法返回null，否则返回错误信息 <br/>
    /// 该方法只会在移动游戏对话框中被调用
    /// </summary>
    /// <param name="moveIn">要移入的库</param>
    /// <param name="moveOut">要移出的库</param>
    /// <param name="galgame">要移动的游戏</param>
    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame);
    
    /// <summary>
    /// 让玩家选择属于该库的路径，应该自行判断该路径是否合法，也自行实现选择方式（界面）<br/>
    /// 如果取消选择则返回null，路径不合法应抛出异常
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    /// <exception cref="PvnException">路径不合法</exception>
    public Task<string?> SelectPathInSourceAsync(GalgameSourceBase source);
}