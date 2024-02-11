namespace GalgameManager.Contracts.Services;

public interface IInfoService
{
    /// <summary>
    /// 记录并通知事件
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="exception">与之相关的异常，若不是异常则不填</param>
    /// <param name="msg"></param>
    public void Event(string eventName, Exception? exception = null, string? msg = null);
}