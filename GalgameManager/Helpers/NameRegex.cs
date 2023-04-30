using System.Text.RegularExpressions;

namespace GalgameManager.Helpers;

public static class NameRegex
{
    /// <summary>
    /// 使用正则表达式获取游戏名
    /// </summary>
    /// <param name="targetString">待匹配串</param>
    /// <param name="pattern">正则匹配串</param>
    /// <param name="removeBorder">是否要移除所得子串的边界</param>
    /// <param name="index">要第几个子串</param>
    /// <returns></returns>
    public static string GetName(string targetString, string pattern, bool removeBorder, int index)
    {
        var result = string.Empty;
        Regex regex = new(pattern);
        MatchCollection match = regex.Matches(targetString);
        if (match.Count > index)
        {
            result = match[index].Value;
            if (removeBorder)
                result = result.Substring(1, result.Length - 2);
        }
        return result;
    }
}
