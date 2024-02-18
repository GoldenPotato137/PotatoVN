namespace GalgameManager.Contracts.Services;

public interface IAppCenterService
{
    /// <summary>
    /// 试图启动 AppCenter 服务，如果已经启动则不会重复启动，如果设置中禁止上传信息则不会启动
    /// </summary>
    public Task StartAsync();

    /// <summary>
    /// 记录异常
    /// </summary>
    public void UploadError(Exception exception);

    /// <summary>
    /// 记录事件
    /// </summary>
    public void UploadEvent(string eventName, Exception? exception = null, string? msg = null);
}