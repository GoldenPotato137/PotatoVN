namespace GalgameManager.Contracts.Services;

public interface IUpdateService
{
    /// <summary>
    /// 是否应该显示更新内容(每个版本只显示一次)
    /// </summary>
    public bool ShouldDisplayUpdateContent();
    
    /// <summary>
    /// 获取更新内容
    /// </summary>
    /// <returns>更新内容</returns>
    public Task<string> GetUpdateContentAsync();
}