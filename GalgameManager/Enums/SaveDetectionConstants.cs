namespace GalgameManager.Enums;

/// <summary>
/// 存档检测相关的常量定义
/// </summary>
public static class SaveDetectionConstants
{
    /// <summary>
    /// 存档文件扩展名（不带点号）
    /// </summary>
    public static readonly HashSet<string> SaveFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sav", "dat", "save", "sfs", "rpgsave", "rvdata", "rvdata2",
        // 可扩展更多文件类型
        "json", "xml", "yaml", "yml", "cfg", "config", "backup"
    };

    /// <summary>
    /// 存档文件关键词
    /// </summary>
    public static readonly HashSet<string> SaveFileKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "save", "sav", "slot", "data", "record", "progress", "file",
        // 可扩展更多关键词
        "state", "status", "profile", "account", "user", "game",
        "session", "checkpoint", "quick", "auto"
    };

    /// <summary>
    /// 特定游戏开发商的已知变体
    /// </summary>
    public static readonly Dictionary<string, List<string>> DeveloperVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["asa"] = new List<string>
        {
            "asaproject", "asa project", "asa_project", "asa-project",
            "AsaProject", "ASA Project", "ASA_PROJECT", "asaproj",
            "asa_proj", "asa-proj", "AsaProj", "asaprojects",
            "asa projects", "asa_projects", "asa-projects"
        },
        ["key"] = new List<string>
        {
            "key", "visualarts", "visual arts", "visual_arts"
        },
        ["typemoon"] = new List<string>
        {
            "typemoon", "type-moon", "type_moon"
        }
    };

    /// <summary>
    /// 常见词汇的简化映射
    /// </summary>
    public static readonly Dictionary<string, List<string>> WordSimplifications = new(StringComparer.OrdinalIgnoreCase)
    {
        ["project"] = new List<string> { "proj", "p" },
        ["game"] = new List<string> { "gm" },
        ["visual"] = new List<string> { "vis" },
        ["novel"] = new List<string> { "vn", "nov" },
        ["story"] = new List<string> { "stry", "st" },
        ["adventure"] = new List<string> { "adv", "adven" },
        ["chronicles"] = new List<string> { "chron", "chr" },
        ["legend"] = new List<string> { "leg", "lgd" },
        ["fantasy"] = new List<string> { "fan", "fnt" },
        ["world"] = new List<string> { "wrld", "wd" }
    };

    /// <summary>
    /// 日语词汇的罗马音映射
    /// </summary>
    public static readonly Dictionary<string, List<string>> JapaneseMappings = new()
    {
        ["プロジェクト"] = new List<string> { "project", "purojekuto" },
        ["ウォーズ"] = new List<string> { "wars", "waruzu" },
        ["ストーリー"] = new List<string> { "story", "story", "sutori" },
        ["ファンタジー"] = new List<string> { "fantasy", "fantesi" },
        ["アドベンチャー"] = new List<string> { "adventure", "adobencha" },
        ["クロニクル"] = new List<string> { "chronicle", "kuronikuru" }
    };

    /// <summary>
    /// 数字映射（阿拉伯数字转中文）
    /// </summary>
    public static readonly Dictionary<string, string> NumberMappings = new()
    {
        ["0"] = "零", ["1"] = "一", ["2"] = "二", ["3"] = "三",
        ["4"] = "四", ["5"] = "五", ["6"] = "六", ["7"] = "七",
        ["8"] = "八", ["9"] = "九", ["10"] = "十"
    };

    /// <summary>
    /// 常见的存档目录模式
    /// </summary>
    public static readonly string[] SaveDirectoryPatterns =
    {
        "save", "saves", "savedata", "save_data", "userdata", "user_data",
        "data", "game", "games", "appdata", "local", "roaming"
    };

    /// <summary>
    /// 汉化文件夹后缀模式（用于识别汉化版本的存档目录）
    /// </summary>
    public static readonly string[] ChineseLocalizationSuffixes =
    {
        "chs", "cht", "cn", "zh", "zhcn", "zhtw", "sc", "tc", "chinese",
        "简体", "繁体", "中文", "汉化", "汉化版", "steam简中"
    };

    /// <summary>
    /// 存档目录常见末尾字符模式（用于更灵活的匹配）
    /// </summary>
    public static readonly string[] SaveDirectorySuffixPatterns =
    {
        // 英文后缀
        "data", "save", "saves", "games", "game", "user", "profile", "config",
        "settings", "storage", "backup", "cache", "temp", "local", "roaming",

        // 汉化后缀
        "存档", "保存", "数据", "配置", "设置", "档案", "记录", "进度",

        // 常见游戏特定后缀
        "system", "content", "resources", "assets", "files", "documents",

        // 日语相关
        "セーブ", "データ", "設定", "システム", "コンフィグ"
    };

    /// <summary>
    /// 特殊路径加分配置
    /// </summary>
    public static readonly Dictionary<string, int> SpecialPathScores = new(StringComparer.OrdinalIgnoreCase)
    {
        ["appdata\\roaming"] = 15,
        ["appdata\\local"] = 12,
        ["my games"] = 10,
        ["saved games"] = 10
    };

    /// <summary>
    /// 分隔符定义
    /// </summary>
    public static readonly char[] Separators = { ' ', '_', '-', '.', '\0' };

    /// <summary>
    /// 当前分隔符（用于分割）
    /// </summary>
    public static readonly char[] CurrentSeparators = { ' ', '_', '-', '.' };

    /// <summary>
    /// 排除路径关键词（这些路径通常包含程序文件而非存档文件）
    /// </summary>
    public static readonly HashSet<string> ExcludePathKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // PotatoVN相关路径
        "potatovn", ".potatovn", "potato vn", "potato-vn", "potato_vn",

        // Windows系统目录
        "windows", "system32", "syswow64", "drivers", "driverstore", "servicing",
        "Microsoft", "win", "winsxs", "temp", "tmp", "Temp", "Packages",

        // 程序安装目录
        "program files", "program files (x86)", "programdata", "program files (arm)", "program files (arm64)",

        // 游戏平台相关
        "steam", "steamapps", "epic games", "uplay", "ubisoft", "origin", "ea games", "gog",
        "battle.net", "blizzard", "riot games", "discord",

        // 开发工具目录
        "visual studio", "msbuild", "nuget", "dotnet", "sdk", "code", "vscode", "android", "android sdk",

        // 浏览器目录
        "google", "chrome", "mozilla", "firefox", "edge", "opera",

        // 社交通信软件
        "xwechat_files","Tencent Files", "WeChat", "QQ", "WeChat Files",

        // 硬件相关目录
        "nvidia", "amd", "intel", "cuda", "rocm",

        // 临时和缓存目录
        "cache", "caches", "temporary", "logs", "log", "recycle bin", "$recycle.bin", "system volume information",

        // 配置和系统文件目录
        "config", "configuration", "settings", "preferences", "registry", "repair", "backup", "system restore",

        // 用户特定应用数据（非游戏相关）
        "microsoft office", "office", "adobe", "photoshop", "autodesk", "skype", "teams", "zoom", "slack", "spotify", "itunes",

        // 防病毒和安全软件
        "kaspersky", "norton", "mcafee", "avast", "avg",

        // 其他系统工具
        "powershell", "cmd", "sysnative", "tasks", "startup", "start menu", "desktop", "wallpaper",

        // 其他软件
        "scoop", "chocolatey", "winget", "TrafficMonitor", "ditto"
    };

    /// <summary>
    /// 检查扩展名是否为存档文件扩展名
    /// </summary>
    /// <param name="extension">不带点号的扩展名</param>
    /// <returns>是否为存档文件扩展名</returns>
    public static bool IsSaveFileExtension(ReadOnlySpan<char> extension)
    {
        if (extension.Length == 0) return false;

        // 转换为字符串后检查HashSet，这是O(1)操作
        return SaveFileExtensions.Contains(extension.ToString());
    }

    /// <summary>
    /// 检查文件名是否包含存档关键词
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>是否包含存档关键词</returns>
    public static bool ContainsSaveKeyword(ReadOnlySpan<char> fileName)
    {
        if (fileName.Length == 0) return false;

        // 逐个检查关键词，一旦找到匹配就返回
        foreach (var keyword in SaveFileKeywords)
        {
            if (fileName.Contains(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 获取指定开发商的变体列表
    /// </summary>
    /// <param name="developer">开发商名称</param>
    /// <returns>变体列表</returns>
    public static List<string> GetDeveloperVariants(string developer)
    {
        if (string.IsNullOrEmpty(developer)) return new List<string>();

        var lowerDev = developer.ToLowerInvariant();

        foreach (var kvp in DeveloperVariants)
        {
            if (lowerDev.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return new List<string>();
    }

    /// <summary>
    /// 应用词汇简化
    /// </summary>
    /// <param name="name">原始名称</param>
    /// <param name="variants">变体集合</param>
    public static void ApplyWordSimplifications(string name, ISet<string> variants)
    {
        var lowerName = name.ToLowerInvariant();

        foreach (var simplification in WordSimplifications)
        {
            if (lowerName.Contains(simplification.Key))
            {
                foreach (var replacement in simplification.Value)
                {
                    var simplified = lowerName.Replace(simplification.Key, replacement);
                    variants.Add(simplified);

                    // 添加无分隔符版本
                    var noSepVersion = simplified.Replace("_", "")
                        .Replace("-", "")
                        .Replace(" ", "");
                    if (noSepVersion != simplified)
                    {
                        variants.Add(noSepVersion);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 应用日语词汇转换
    /// </summary>
    /// <param name="name">原始名称</param>
    /// <param name="variants">变体集合</param>
    public static void ApplyJapaneseConversions(string name, ISet<string> variants)
    {
        foreach (var mapping in JapaneseMappings)
        {
            if (name.Contains(mapping.Key))
            {
                foreach (var variant in mapping.Value)
                {
                    var converted = name.ToLowerInvariant().Replace(mapping.Key, variant);
                    variants.Add(converted);
                }
            }
        }
    }

    /// <summary>
    /// 应用数字转换
    /// </summary>
    /// <param name="name">原始名称</param>
    /// <returns>转换后的名称列表</returns>
    public static List<string> ApplyNumberConversions(string name)
    {
        var variants = new List<string>();
        var result = name;

        foreach (var mapping in NumberMappings)
        {
            result = result.Replace(mapping.Key, mapping.Value);
            variants.Add(result);
        }

        return variants;
    }

    /// <summary>
    /// 检查路径是否符合存档目录模式
    /// </summary>
    /// <param name="directory">目录路径</param>
    /// <returns>路径评分</returns>
    public static int GetPathStructureScore(string directory)
    {
        var score = 0;
        var dirLower = directory.ToLowerInvariant();

        // 检查常见存档模式
        foreach (var pattern in SaveDirectoryPatterns)
        {
            if (dirLower.Contains(pattern))
            {
                score += 8;
            }
        }

        // 检查汉化文件夹后缀（加分较高，因为这些通常表示游戏特定的存档目录）
        foreach (var suffix in ChineseLocalizationSuffixes)
        {
            if (dirLower.EndsWith(suffix.ToLowerInvariant()) ||
                dirLower.Contains($"_{suffix.ToLowerInvariant()}") ||
                dirLower.Contains($"-{suffix.ToLowerInvariant()}"))
            {
                score += 12; // 汉化目录加分更高
            }
        }

        // 检查目录末尾字符模式（更灵活的匹配）
        foreach (var suffix in SaveDirectorySuffixPatterns)
        {
            if (dirLower.EndsWith(suffix.ToLowerInvariant()))
            {
                score += 6; // 末尾匹配加分中等
            }
            else if (dirLower.Contains($"_{suffix.ToLowerInvariant()}") ||
                     dirLower.Contains($"-{suffix.ToLowerInvariant()}") ||
                     dirLower.Contains($".{suffix.ToLowerInvariant()}"))
            {
                score += 4; // 包含加分较低
            }
        }

        // 检查特殊路径加分
        foreach (var pattern in SpecialPathScores)
        {
            if (dirLower.Contains(pattern.Key))
            {
                score += pattern.Value;
            }
        }

        return score;
    }

    /// <summary>
    /// 检查路径是否应该被排除（避免扫描程序路径）
    /// </summary>
    /// <param name="targetPath">要检查的路径</param>
    /// <param name="currentAppPath">当前应用程序路径</param>
    /// <returns>是否应该排除此路径</returns>
    public static bool ShouldExcludePath(string targetPath, string currentAppPath)
    {
        if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(currentAppPath))
            return false;

        var targetLower = targetPath.ToLowerInvariant();

        // 检查是否包含排除关键词
        foreach (var keyword in ExcludePathKeywords)
        {
            // 修复：必须将 keyword 也转为小写，否则类似 "TrafficMonitor" 无法匹配 "trafficmonitor"
            if (targetLower.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        // 排除在当前程序路径下的路径
        if (targetPath.StartsWith(currentAppPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取便携式路径（使用环境变量或相对路径）
    /// </summary>
    public static string GetPortablePath(string path, string? localPath)
    {
        if (string.IsNullOrEmpty(path)) return path;

        // 1. Local Path (Relative)
        if (!string.IsNullOrEmpty(localPath) && path.StartsWith(localPath, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(localPath, path);
        }

        // 2. Special Folders
        var mappings = new Dictionary<string, string>
        {
            { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%AppData%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LocalAppData%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "%Documents%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%UserProfile%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("Local", "LocalLow"), "%LocalLow%" }
        };

        foreach (var mapping in mappings.OrderByDescending(m => m.Key.Length))
        {
            if (path.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (path.Length == mapping.Key.Length) return mapping.Value;
                
                if (path[mapping.Key.Length] == Path.DirectorySeparatorChar || path[mapping.Key.Length] == Path.AltDirectorySeparatorChar)
                {
                     var relative = path.Substring(mapping.Key.Length + 1);
                     return $"{mapping.Value}/{relative}".Replace('\\', '/');
                }
            }
        }

        return path;
    }

    /// <summary>
    /// 获取绝对路径（解析环境变量和相对路径）
    /// </summary>
    public static string GetAbsolutePath(string? path, string? localPath)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        
        var result = path;
        
        // 1. Special Folders
        result = result.Replace("%AppData%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase)
                       .Replace("%LocalAppData%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase)
                       .Replace("%Documents%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase)
                       .Replace("%UserProfile%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase)
                       .Replace("%LocalLow%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("Local", "LocalLow"), StringComparison.OrdinalIgnoreCase);

        // 2. Relative Path
        if (!Path.IsPathRooted(result) && !string.IsNullOrEmpty(localPath))
        {
            try 
            {
                result = Path.GetFullPath(Path.Combine(localPath, result));
            }
            catch
            {
                // ignore invalid path combination
            }
        }
        
        return result;
    }
}