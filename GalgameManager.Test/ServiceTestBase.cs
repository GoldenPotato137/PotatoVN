using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Windows.Storage;

namespace GalgameManager.Test;

/// <summary>
/// Service单元测试基座：提供真实LiteDB（每测试独立临时文件）、内存设置存储与常用mock，
/// 使客户端Service可以在普通NUnit进程中实例化。<br/>
/// 依赖的生产侧接缝：UiThreadInvokeHelper未初始化时内联执行、SourceServiceFactory.SetResolverForTest、
/// Service通过IServiceProvider懒解析循环依赖。
/// </summary>
public abstract class ServiceTestBase
{
    protected string TestDir = null!;
    protected LiteDatabase Database = null!;
    protected FakeLocalSettingsService Settings = null!;
    protected Mock<IInfoService> InfoService = null!;
    protected Mock<IBgTaskService> BgTaskService = null!;
    protected Mock<IGalgameCollectionService> GalgameCollectionService = null!;

    [SetUp]
    public void ServiceTestBaseSetUp()
    {
        TestDir = Path.Combine(TestEnvironmentSetup.Root, TestContext.CurrentContext.Test.Name);
        Directory.CreateDirectory(TestDir);
        // 与 LocalSettingsService.InitDatabase 保持一致的全局配置
        BsonMapper.Global.EnumAsInteger = true;
        BsonMapper.Global.RegisterType<Version>(
            serialize: v => v.ToString(),
            deserialize: b => Version.Parse(b.AsString));
        Database = new LiteDatabase(Path.Combine(TestDir, "data.db"));
        Settings = new FakeLocalSettingsService(Database, TestDir);
        InfoService = new Mock<IInfoService>();
        BgTaskService = new Mock<IBgTaskService>();
        BgTaskService.Setup(x => x.AddBgTask(It.IsAny<BgTaskBase>())).Returns(Task.CompletedTask);
        GalgameCollectionService = new Mock<IGalgameCollectionService>();
        GalgameCollectionService.SetupGet(x => x.Galgames).Returns(new ObservableCollection<Galgame>());
        GalgameCollectionService.Setup(x => x.SaveGalgameAsync(It.IsAny<Galgame>())).Returns(Task.CompletedTask);
        GalgameCollectionService.Setup(x => x.SaveGalgamesAsync()).Returns(Task.CompletedTask);
        GalgameCollectionService.Setup(x => x.RemoveGalgame(It.IsAny<Galgame>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        Mock<IGalgameSourceService> sourceServiceStub = new();
        sourceServiceStub.Setup(x => x.AddListenAsync(It.IsAny<GalgameSourceBase>())).Returns(Task.CompletedTask);
        sourceServiceStub.Setup(x => x.RemoveListenAsync(It.IsAny<GalgameSourceBase>())).Returns(Task.CompletedTask);
        SourceServiceFactory.SetResolverForTest(_ => sourceServiceStub.Object);
    }

    [TearDown]
    public void ServiceTestBaseTearDown()
    {
        SourceServiceFactory.SetResolverForTest(null);
        Database.Dispose();
        try
        {
            if (Directory.Exists(TestDir)) Directory.Delete(TestDir, recursive: true);
        }
        catch
        {
            // ignore
        }
    }

    /// 用真实ServiceCollection构造IServiceProvider，已注册mock的IGalgameCollectionService
    protected IServiceProvider CreateServiceProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton(GalgameCollectionService.Object);
        return services.BuildServiceProvider();
    }

    protected string CreateDir(string relativePath)
    {
        var path = Path.Combine(TestDir, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    /// 轮询等待条件满足（用于fire-and-forget路径，无法直接await），超时则断言失败
    protected static async Task WaitUntilAsync(Func<bool> condition, string failMessage)
    {
        for (var i = 0; i < 50; i++)
        {
            if (condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail(failMessage);
    }
}

/// <summary>
/// 内存版ILocalSettingsService：设置存Dictionary（经Newtonsoft序列化往返以模拟真实行为），
/// Database为真实LiteDB。未实现的成员抛NotImplementedException，用到再补。
/// </summary>
public class FakeLocalSettingsService : ILocalSettingsService
{
    private readonly Dictionary<string, string> _settings = new();

    public FakeLocalSettingsService(LiteDatabase database, string rootDir)
    {
        Database = database;
        LocalFolder = new DirectoryInfo(Path.Combine(rootDir, "Local"));
        TemporaryFolder = new DirectoryInfo(Path.Combine(rootDir, "Temp"));
    }

    public event ILocalSettingsService.Delegate? OnSettingChanged
    {
        add { }
        remove { }
    }

    public DirectoryInfo LocalFolder { get; }

    public DirectoryInfo TemporaryFolder { get; }

    public LiteDatabase Database { get; }

    public bool IsDatabaseUsable => true;

    public void InitDatabase()
    {
    }

    public void InitSettingDatabase()
    {
    }

    public Task<T?> ReadSettingAsync<T>(string key, bool isLarge = false, List<JsonConverter>? converters = null,
        bool typeNameHandling = false)
    {
        if (!_settings.TryGetValue(key, out var json)) return Task.FromResult<T?>(default);
        return Task.FromResult(JsonConvert.DeserializeObject<T>(json, CreateSerializerSettings(converters,
            typeNameHandling)));
    }

    public Task<T?> ReadOldSettingAsync<T>(string key, T template, JsonSerializerSettings? settings = null)
        => Task.FromResult<T?>(default);

    public Task SaveSettingAsync<T>(string key, T value, bool isLarge = false, bool triggerEventWhenNull = false,
        List<JsonConverter>? converters = null, bool typeNameHandling = false)
    {
        _settings[key] = JsonConvert.SerializeObject(value, CreateSerializerSettings(converters, typeNameHandling));
        return Task.CompletedTask;
    }

    public Task RemoveSettingAsync(string key, bool isLarge = false)
    {
        _settings.Remove(key);
        return Task.CompletedTask;
    }

    public Task<string?> AddImageToExportAsync(string? imagePath) => Task.FromResult(imagePath);

    public Task<string?> GetImageFromImportAsync(string? imagePath) => Task.FromResult(imagePath);

    /// 已导出的数据，key为导出项名
    public Dictionary<string, object> Exported { get; } = new();

    public Task AddToExportAsync(string key, object value, List<JsonConverter>? converters = null,
        bool typeNameHandling = false)
    {
        Exported[key] = value;
        return Task.CompletedTask;
    }

    public Task AddToExportDirectlyAsync(string key) => throw new NotImplementedException();

    public Task<StorageFolder> GetTmpExportFolder() => throw new NotImplementedException();

    public Task<string> BackupFailedDataAsync(bool removeAfterBackup = true) => throw new NotImplementedException();

    public Task StartupAsync() => Task.CompletedTask;

    public Task ImportPageSettingsAsync() => Task.CompletedTask;

    private static JsonSerializerSettings CreateSerializerSettings(List<JsonConverter>? converters,
        bool typeNameHandling)
    {
        JsonSerializerSettings settings = new();
        if (converters is not null) settings.Converters = converters;
        if (typeNameHandling) settings.TypeNameHandling = TypeNameHandling.All;
        return settings;
    }
}
