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
}