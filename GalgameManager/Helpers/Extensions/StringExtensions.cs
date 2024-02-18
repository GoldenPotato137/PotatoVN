namespace GalgameManager.Helpers;

public static class StringExtensions
{
    /// <summary>
    /// 计算两个字符串的相似度
    /// </summary>
    /// <returns>jaro-winkler距离: [0,1]</returns>
    public static double JaroWinkler(this string str1, string str2)
    {
        str1 = str1.ToLower();
        str2 = str2.ToLower();
        if(str1.Length > str2.Length)
            (str1, str2) = (str2, str1);
        int n = str1.Length, m = str2.Length, range = Math.Max(m / 2 - 1, 0);
        int match = 0, swap = 0;
        for (var i = 0; i < n; i++)
        {
            if (str1[i] == str2[i])
            {
                match++;
                continue;
            }
            int matched = 0, swapped = 0;
            for(var j = Math.Max(0, i-range); j < Math.Min(m, i+range+1); j++)
                if (i!= j && str1[i] == str2[j])
                {
                    matched = 1;
                    if (i != j && j < n && str1[j] == str2[i])
                        swapped = 1;
                    if(swapped == 1)
                        break;
                }
            swap += swapped;
            match += matched;
        }
        if (match == 0)
            return 0;
        return (match / (double)n + match / (double)m + (match - swap / 2.0) / match) / 3.0;
    }
    
    /// <summary>
    /// 计算两个字符串的编辑距离
    /// </summary>
    public static int Levenshtein(this string s1, string s2)
    {
        s1 = s1.ToLower();
        s2 = s2.ToLower();
        int n = s1.Length, m = s2.Length;
        var dp = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++)
            dp[i, 0] = i;
        for (var j = 0; j <= m; j++)
            dp[0, j] = j;
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                dp[i, j] = Math.Min(dp[i - 1, j], dp[i, j - 1]) + 1;
                dp[i, j] = Math.Min(dp[i, j], dp[i - 1, j - 1] + (s1[i - 1] == s2[j - 1] ? 0 : 1));
            }
        }

        return dp[n, m];
    }
    
    public static bool IsNullOrEmpty(this string? str) => string.IsNullOrEmpty(str);
}