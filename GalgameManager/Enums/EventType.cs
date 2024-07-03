namespace GalgameManager.Enums;

public enum EventType
{
    PvnSyncEvent,
    PvnSyncEmptyEvent,
    PvnAccountEvent,
    BgmOAuthEvent, // Bgm账号相关的事件
    GalgameEvent,
    FaqEvent,
    AppError,
    /// <summary>
    /// 不严重的意外错误，这类事件只会在打开开发者模式时通知
    /// </summary>
    NotCriticalUnexpectedError,
    
    /// <summary>
    /// 后台任务失败触发的事件
    /// </summary>
    BgTaskFailEvent,
    
    /// <summary>
    /// 后台任务成功触发的事件
    /// </summary>
    BgTaskSuccessEvent,
}