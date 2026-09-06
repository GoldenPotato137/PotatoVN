namespace GalgameManager.Server.Models;

/// <summary>
/// 搜刮器代理透传结果
/// </summary>
public class ScraperProxyResult(int statusCode, string body, string contentType)
{
    /// <summary>上游HTTP状态码</summary>
    public int StatusCode { get; } = statusCode;
    /// <summary>上游响应正文</summary>
    public string Body { get; } = body;
    /// <summary>上游响应ContentType</summary>
    public string ContentType { get; } = contentType;
}
