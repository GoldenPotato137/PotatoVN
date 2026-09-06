using GalgameManager.Contracts.Services;
using GalgameManager.Models.Sources;
using GalgameManager.Services;

namespace GalgameManager.Helpers;

public static class SourceServiceFactory
{
    private static readonly Dictionary<GalgameSourceType, IGalgameSourceService> SourceServices = new();
    private static readonly object Lock = new();
    private static Func<GalgameSourceType, IGalgameSourceService?>? _testResolver;

    /// <summary>仅测试环境使用：注入自定义resolver以替代App服务定位器，传null恢复默认行为</summary>
    public static void SetResolverForTest(Func<GalgameSourceType, IGalgameSourceService?>? resolver)
    {
        lock (Lock) _testResolver = resolver;
    }

    public static IGalgameSourceService GetSourceService(GalgameSourceType type)
    {
        lock (Lock)
        {
            if (_testResolver?.Invoke(type) is { } testService) return testService;
            if (SourceServices.TryGetValue(type, out IGalgameSourceService? value)) return value;
            value = type switch
            {
                GalgameSourceType.LocalFolder => App.GetService<LocalFolderSourceService>(),
                GalgameSourceType.UnKnown => throw new ArgumentException("UnKnow source"),
                GalgameSourceType.LocalZip => App.GetService<ZipSourceService>(),
                GalgameSourceType.Virtual => App.GetService<VirtualSourceService>(),
                GalgameSourceType.Steam => App.GetService<SteamSourceService>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            SourceServices[type] = value;
            return value;
        }
    }
}