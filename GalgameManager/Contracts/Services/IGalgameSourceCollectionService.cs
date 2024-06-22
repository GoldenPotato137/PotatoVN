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

    public GalgameSourceBase? GetGalgameSourceFromUrl(string url);
}