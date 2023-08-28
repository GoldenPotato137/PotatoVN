using System.Drawing.Text;
using System.Net.Http.Headers;
using Windows.Foundation;

namespace GalgameManager.Helpers;

public static class Utils
{
    public static string GetFirstValueByNameOrEmpty(this WwwFormUrlDecoder decoder, string name)
    {
        try
        {
            return decoder.GetFirstValueByName(name);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 检查字体是否安装
    /// </summary>
    /// <param name="fontName">字体名</param>
    public static bool IsFontInstalled(string fontName)
    {
        InstalledFontCollection fontsCollection = new();
        return fontsCollection.Families.Any(font => font.Name.Equals(fontName, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// 获取软件默认HttpClient
    /// </summary>
    /// <returns></returns>
    public static HttpClient GetDefaultHttpClient()
    {
        HttpClient client = new();
        var version = RuntimeHelper.GetVersion();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", 
            $"GoldenPotato/PotatoVN/{version} (Windows) (https://github.com/GoldenPotato137/PotatoVN)");
        return client;
    }

    /// <summary>
    /// 清除请求头的accept，并添加application/json
    /// </summary>
    public static HttpClient WithApplicationJson(this HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}