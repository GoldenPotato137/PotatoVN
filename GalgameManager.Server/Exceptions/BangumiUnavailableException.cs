namespace GalgameManager.Server.Exceptions;

/// <summary>无法从bgm获取数据（如网络错误、bgm服务器返回异常响应）</summary>
public class BangumiUnavailableException(string msg, Exception inner) : Exception(msg, inner);