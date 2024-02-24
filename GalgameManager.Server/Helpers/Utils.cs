namespace GalgameManager.Server.Helpers;

public static class Utils
{
    /// <summary>
    /// 获取服务端默认HttpClient
    /// </summary>
    /// <returns></returns>
    public static HttpClient GetDefaultHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            $"GoldenPotato/PotatoVN.Server (AnyPlatform/Docker) (https://github.com/GoldenPotato137/PotatoVN)");
        return client;
    }
}