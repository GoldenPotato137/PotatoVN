using Windows.Storage;

namespace GalgameManager.Helpers;

public static class AppStoragePaths
{
    // 使用环境变量强制指定存储路径，优先级最高
    private const string LocalDataPathOverrideEnvVar = "POTATOVN_LOCALDATA_PATH";
    private const string TempPathOverrideEnvVar = "POTATOVN_TEMP_PATH";
    private const string UpgradeUiTestEnvVar = "POTATOVN_UPGRADE_UI_TEST";

    private const string PortableEnvVar = "POTATOVN_PORTABLE";
    private const string PortableFlagFileName = "portable.flag";
    private const string PortableDisableFlagFileName = "portable.disable";

    private const string PortableDataFolderName = "UserData";
    private const string PortableTempFolderName = "Temp";

    private static readonly Lazy<bool> _isPortable = new(ComputeIsPortable);
    private static readonly Lazy<string> _localDataPath = new(ComputeLocalDataPath);
    private static readonly Lazy<string> _tempPath = new(ComputeTempPath);

    public static bool IsPortable => _isPortable.Value;

    public static string LocalDataPath => _localDataPath.Value;

    public static string TempPath => _tempPath.Value;

    /// <summary>
    /// 是否正在运行旧数据升级 UI 端到端测试。
    /// </summary>
    public static bool IsUpgradeUiTest
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(UpgradeUiTestEnvVar);
            return !string.IsNullOrWhiteSpace(value) && IsTrue(value);
        }
    }

    private static bool ComputeIsPortable()
    {
        // MSIX 应用无法写入安装目录，因此不支持便携模式
        if (RuntimeHelper.IsMSIX) return false;

        var baseDir = AppContext.BaseDirectory;
        var disableFlag = Path.Combine(baseDir, PortableDisableFlagFileName);
        if (File.Exists(disableFlag)) return false;

        var env = Environment.GetEnvironmentVariable(PortableEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (IsTrue(env)) return true;
            if (IsFalse(env)) return false;
        }

        var flag = Path.Combine(baseDir, PortableFlagFileName);
        if (File.Exists(flag)) return true;

        // 如果应用目录可写（不在Program Files下），则启用便携模式
        var candidate = Path.Combine(baseDir, PortableDataFolderName);
        return CanWriteToFolder(candidate);
    }

    private static string ComputeLocalDataPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(LocalDataPathOverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath)) return EnsureDirectory(overridePath.Trim());

        if (IsPortable) return EnsureDirectory(Path.Combine(AppContext.BaseDirectory, PortableDataFolderName));
        // 非便携：优先使用包身份提供的 LocalFolder；无包身份时退回到 LocalAppData（不太可能发生）
        if (RuntimeHelper.IsMSIX) return EnsureDirectory(ApplicationData.Current.LocalFolder.Path);

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(root, "PotatoVN");
        return EnsureDirectory(folder);
    }

    private static string ComputeTempPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(TempPathOverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath)) return EnsureDirectory(overridePath.Trim());

        if (IsPortable) return EnsureDirectory(Path.Combine(LocalDataPath, "..", PortableTempFolderName));
        if (RuntimeHelper.IsMSIX) return EnsureDirectory(ApplicationData.Current.TemporaryFolder.Path);
        // 无包身份：使用数据目录下的 Temp
        return EnsureDirectory(Path.Combine(LocalDataPath, PortableTempFolderName));
    }

    private static bool CanWriteToFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var testFile = Path.Combine(folder, ".write_test");
            File.WriteAllText(testFile, "1");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureDirectory(string path)
    {
        DirectoryInfo di = new(path);
        if (!di.Exists) di.Create();
        return di.FullName;
    }

    private static bool IsTrue(string value)
    {
        value = value.Trim();
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFalse(string value)
    {
        value = value.Trim();
        return value.Equals("0", StringComparison.OrdinalIgnoreCase)
               || value.Equals("false", StringComparison.OrdinalIgnoreCase)
               || value.Equals("no", StringComparison.OrdinalIgnoreCase)
               || value.Equals("off", StringComparison.OrdinalIgnoreCase);
    }
}
