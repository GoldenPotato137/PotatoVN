using System.Drawing.Text;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
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

    /// <summary>
    /// 获取本机Mac地址
    /// </summary>
    /// <returns>若没有则返回空string</returns>
    public static string GetMacAddress()
    {
        foreach(NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus == OperationalStatus.Up)
                return nic.GetPhysicalAddress().ToString();
        }
        return string.Empty;
    }

    /// <summary>
    /// 检查能否访问互联网
    /// </summary>
    public static async Task<bool> CheckInternetConnection()
    {
        try
        {
            HttpResponseMessage tmp = await GetDefaultHttpClient().GetAsync("https://www.baidu.com");
            return tmp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    public static string ToBase64(this string str) => Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
    
    public static string FromBase64(string str) => Encoding.UTF8.GetString(Convert.FromBase64String(str));
}