namespace GalgameManager.Contracts.Services;

public interface IAutoExportService
{
    /// <summary>
    /// 启动自动导出调度。重复调用不会创建多个调度循环。
    /// </summary>
    void Start();

    /// <summary>
    /// 停止自动导出调度，不会中断已经开始的导出任务。
    /// </summary>
    void Stop();

    /// <summary>
    /// 保存自动导出开关；启用时会先验证导出路径。
    /// </summary>
    /// <returns>设置是否成功生效。</returns>
    Task<bool> SetEnabledAsync(bool enabled);

    /// <summary>
    /// 启动一次手动导出；已有导出任务运行时返回 false。
    /// </summary>
    Task<bool> ExportAsync(string targetPath);
}
