using GalgameManager.Contracts.Services;

namespace GalgameManager.Services;

/// <summary>
/// 消息及异常记录与通知服务<br/>
/// 未来会加入消息通知功能，目前仅记录异常
/// </summary>
public class InfoService : IInfoService
{
    private readonly IAppCenterService _appCenterService;
    
    public InfoService(IAppCenterService appCenterService)
    {
        _appCenterService = appCenterService;
    }
    
    public void Event(string eventName, Exception? exception = null, string? msg = null)
    {
        _appCenterService.UploadEvent(eventName, exception, msg);
    }
}