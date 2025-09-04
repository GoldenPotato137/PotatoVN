using System.Drawing.Text;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Web;
using Windows.Foundation;
using GalgameManager.Models;
using HtmlAgilityPack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using TinyPinyin;

namespace GalgameManager.Helpers;

public static partial class Utils
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
    /// 罗马化日语支持 eg: ゆずソフト 与 yuzusofuto
    /// </summary>
    /// <param name="self"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static bool ContainX(this string self, string target)
    {
        if (self == null || target == null)
            return false;
        self = self.ToLower().Replace(" ",string.Empty);
        target = target.ToLower().Replace(" ",string.Empty);
        if (self.Contains(target)) return true;
        if (IsFullyAscii(target) == false) return false;
        if (PinyinHelper.GetPinyinInitials(self).ToLower().Contains(target)) return true;
        if (PinyinHelper.GetPinyin(self, string.Empty).ToLower().Contains(target)) return true;
        if (WanaKanaShaapu.WanaKana.ToRomaji(self).ToLower().Contains(target)) return true;
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
    
    /// <summary>
    /// 检查两个系统路径或符合Uri格式的路径（如ftp://123/a/b/c）是否相同
    /// </summary>
    /// <param name="path1"></param>
    /// <param name="path2"></param>
    /// <returns></returns>
    public static bool ArePathsEqual(string path1, string path2)
    {
        if (Uri.TryCreate(path1, UriKind.Absolute, out Uri? tmp1) &&
            Uri.TryCreate(path2, UriKind.Absolute, out Uri? tmp2))
            return tmp1.Equals(tmp2);
        
        Uri uri1 = new(Path.GetFullPath(path1), UriKind.Absolute);
        Uri uri2 = new(Path.GetFullPath(path2), UriKind.Absolute);
        return uri1.Equals(uri2);
    }

    /// <summary>
    /// 检查一个路径是否包含在另一个路径中
    /// </summary>
    /// <param name="parentPath"></param>
    /// <param name="childPath"></param>
    /// <returns></returns>
    public static bool IsPathContained(string parentPath, string childPath)
    {
        try
        {
            var parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar);
            var child = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar);
            
            return child.Equals(parent, StringComparison.OrdinalIgnoreCase) || 
                   (child + Path.DirectorySeparatorChar).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查一个路径是否是另一个路径的直接子文件夹
    /// </summary>
    public static bool IsChildFolder(string parentPath, string childPath)
    {
        var tmp = Path.GetDirectoryName(childPath);
        if (tmp is null) return false;
        return Path.GetFullPath(parentPath) == Path.GetFullPath(tmp);
    }
    
    /// <summary>
    /// 尝试解析日期，若失败则返回DateTime.MinValue
    /// </summary>
    public static DateTime TryParseDateGuessCulture(string dateString)
    {
        CultureInfo[] cultures =
        {
            CultureInfo.InvariantCulture,
            new("en-US"), // MM/dd/yyyy
            new("en-GB"), // dd/MM/yyyy
            new("ja-JP"), // yyyy/MM/dd
            new("zh-CN"), // yyyy/M/d
            // 添加其他可能的文化设置
        };
        foreach (CultureInfo culture in cultures)
            if (DateTime.TryParse(dateString, culture, DateTimeStyles.None, out DateTime parsedDate))
                return parsedDate;
        return DateTime.MinValue;
    }
    
    /// <summary>
    /// 检查path是否可写
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool IsPathWritable(string path)
    {
        try
        {
            using FileStream fs = File.Create(Path.Combine(path, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 检查图片是否有效
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    public static bool IsImageValid(string? src)
    {
        if (string.IsNullOrEmpty(src) || src is Galgame.DefaultImagePath) return false;
        return File.Exists(src);
    }

    /// <summary>
    /// 检查WebView2是否可用
    /// </summary>
    /// <returns></returns>
    public static bool IsWebview2Ok()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString() != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string CleanHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        // 移除不需要的标签，比如脚本和样式
        doc.DocumentNode.SelectNodes("//script|//style")?.ToList().ForEach(n => n.Remove());
        StringBuilder sb = new();
        using (StringWriter writer = new(sb))
        {
            ConvertTo(doc.DocumentNode, writer);
        }
        return HttpUtility.HtmlDecode(sb.ToString()); // 解码可能存在的HTML实体，例如 &amp; -> &

        void ConvertTo(HtmlNode node, TextWriter outText)
        {
            switch (node.NodeType)
            {
                case HtmlNodeType.Comment:
                    // 不处理注释
                    break;
                case HtmlNodeType.Document:
                    ConvertChildren(node, outText);
                    break;
                case HtmlNodeType.Text:
                    if (node.ParentNode.Name is "script" or "style") // 如果文本在一个脚本或样式块中，则忽略
                        break;
                    // 获取并清理文本
                    var tmpHtml = ((HtmlTextNode)node).Text;
                    if (HtmlNode.IsOverlappedClosingElement(tmpHtml))
                        break;
                    // 将连续的空白字符替换为单个空格
                    tmpHtml = MultiSpaceRegex().Replace(tmpHtml, " ");
                    if (!string.IsNullOrWhiteSpace(tmpHtml))
                        outText.Write(tmpHtml);
                    break;
                case HtmlNodeType.Element:
                    // 根据标签类型进行格式化处理
                    switch (node.Name.ToLower())
                    {
                        case "p":
                        case "div": // 将div也视作段落
                            // 在段落前添加一个空行以分隔
                            outText.Write("\r\n");
                            ConvertChildren(node, outText);
                            // 在段落后添加一个空行
                            outText.Write("\r\n");
                            break;
                        case "br":
                            outText.Write("\r\n");
                            break;
                        case "li":
                            // 为列表项添加前缀
                            outText.Write(" * ");
                            ConvertChildren(node, outText);
                            // outText.Write("\r\n");
                            break;
                        case "h1":
                        case "h2":
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                            // 标题前后都添加换行
                            outText.Write("\r\n");
                            ConvertChildren(node, outText);
                            outText.Write("\r\n");
                            break;
                        default:
                            // 对于其他标签，只处理其子节点
                            ConvertChildren(node, outText);
                            break;
                    }
                    break;
            }
        }
        
        void ConvertChildren(HtmlNode parent, TextWriter outText)
        {
            foreach (HtmlNode node in parent.ChildNodes)
            {
                ConvertTo(node, outText);
            }
        }

    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s{2,}")]
    private static partial System.Text.RegularExpressions.Regex MultiSpaceRegex();
    
    
    public static bool IsAnyContentDialogOpen()
    {
        WindowEx? window = App.MainWindow;
        if (window == null) return false;
        IReadOnlyList<Popup>? popups = VisualTreeHelper.GetOpenPopups(window);
        return popups.Any(p => p.Child is ContentDialog);
    }
    
    public static async Task WaitForDialogClosedAsync()
    {
        while (IsAnyContentDialogOpen())
            await Task.Delay(200);
    }
}