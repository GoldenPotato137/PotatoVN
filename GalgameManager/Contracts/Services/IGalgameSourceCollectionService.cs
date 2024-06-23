using System.Collections.ObjectModel;
using GalgameManager.Models;
using GalgameManager.Models.Sources;

namespace GalgameManager.Contracts.Services;

public interface IGalgameSourceCollectionService
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <returns></returns>
    Task InitAsync();
    
    /// <summary>
    /// 应用启动后调用
    /// </summary>
    Task StartAsync();

    public ObservableCollection<GalgameSourceBase> GetGalgameSources();

    /// <inheritdoc cref="GetGalgameSource"/>
    public GalgameSourceBase? GetGalgameSourceFromUrl(string url);

    /// 尝试获取某个库，若不存在则返回null
    /// <p>
    /// 对于不同的库的类型，匹配规则如下：<br/>
    /// <list type="bullet">
    /// <item>本地文件夹：若设置中开启了递归搜索子文件节，则匹配path的父文件节；否则直接匹配path</item>
    /// <item>虚拟游戏库：path填什么都行，返回唯一的虚拟库</item>
    /// <item>对于剩余的库，直接匹配path</item>
    /// </list>
    /// </p>
    /// 
    public GalgameSourceBase? GetGalgameSource(GalgameSourceType type, string path);

    /// <summary>
    /// 试图添加一个galgame库
    /// </summary>
    /// <param name="sourceType"></param>
    /// <param name="path">库路径</param>
    /// <param name="tryGetGalgame">是否自动寻找库里游戏</param>
    /// <exception cref="Exception">库已经添加过了</exception>
    public Task<GalgameSourceBase> AddGalgameSourceAsync(GalgameSourceType sourceType, string path,
        bool tryGetGalgame = true);

    /// <summary>
    /// 删除一个galgame库，其包含弹窗警告，若用户取消则什么都不做
    /// </summary>
    /// <param name="source"></param>
    public Task DeleteGalgameFolderAsync(GalgameSourceBase source);

    /// <summary>
    /// 将游戏移入某个库
    /// </summary>
    /// <param name="target">目标库</param>
    /// <param name="game">游戏</param>
    /// <param name="path">目标路径，若为null则表示可以由对应的sourceService自行决定路径</param>
    public Task MoveIntoSourceAsync(GalgameSourceBase target, Galgame game, string? path = null);
    
    /// <summary>
    /// 将游戏移出某个库
    /// </summary>
    public Task RemoveFromSourceAsync(GalgameSourceBase target, Galgame game);
}