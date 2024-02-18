using System.Drawing.Text;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using Windows.Foundation;
using Newtonsoft.Json;
using TinyPinyin;

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
    
    public static HttpClient AddToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

    /// <summary>
    /// self是否包含target，忽略大小写与空格，对于中文串也会比较拼音与拼音首字母
    /// </summary>
    /// <param name="self"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static bool ContainX(this string self, string target)
    {
        self = self.ToLower().Replace(" ",string.Empty);
        target = target.ToLower().Replace(" ",string.Empty);
        if (self.Contains(target)) return true;
        if (IsFullyAscii(target) == false) return false;
        if (PinyinHelper.GetPinyinInitials(self).ToLower().Contains(target)) return true;
        if (PinyinHelper.GetPinyin(self, string.Empty).ToLower().Contains(target)) 
            return true;
        return false;
    }
    
    private static bool IsFullyAscii(string str)
    {
        return str.All(c => c <= 127);
    }

    /// <summary>
    /// self与target比较，忽略大小写、下划线、连字符，空格
    /// </summary>
    public static int CompareX(this string self, string target)
    {
        self = self.ToLower().Replace("_", string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        target = target.ToLower().Replace("_", string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        return string.Compare(self, target, StringComparison.Ordinal);
    }
}