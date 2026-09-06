using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public class GalgameSourceCollectionServiceTest : ServiceTestBase
{
    private GalgameSourceCollectionService CreateService() =>
        new(Settings, BgTaskService.Object, InfoService.Object, CreateServiceProvider());

    private async Task<GalgameSourceCollectionService> CreateInitializedServiceAsync()
    {
        GalgameSourceCollectionService service = CreateService();
        await service.InitAsync();
        return service;
    }

    // 验证全新数据库下InitAsync会跑完所有数据升级：自动创建虚拟游戏库，并把各项升级完成标记持久化到设置中
    [Test]
    public async Task InitAsync_FreshDatabase_CreatesVirtualSource_AndPersistsDataStatus()
    {
        GalgameSourceCollectionService service = await CreateInitializedServiceAsync();

        Assert.That(service.GetGalgameSources().Count(s => s.SourceType == GalgameSourceType.Virtual),
            Is.EqualTo(1));
        LocalSettingStatus? status = await Settings.ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true);
        Assert.That(status, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(status!.GalgameSourceFormatUpgrade, Is.True);
            Assert.That(status.GalgameSourceAddVirtualSource, Is.True);
            Assert.That(status.MetaBackupPerSourceUpgrade, Is.True);
            Assert.That(status.GalgameMultiInstallUpgrade, Is.True);
        });
    }

    // 验证添加一个全新的本地库：库进入集合、可按路径找回，且经UiThreadInvokeHelper降级内联触发的OnSourceChanged事件恰好一次
    [Test]
    public async Task AddGalgameSourceAsync_NewLocalFolder_AddsSource_AndRaisesOnSourceChanged()
    {
        GalgameSourceCollectionService service = await CreateInitializedServiceAsync();
        var raised = 0;
        service.OnSourceChanged += () => raised++;
        string path = CreateDir("lib");

        GalgameSourceBase source = await service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, path,
            tryGetGalgame: false);

        Assert.Multiple(() =>
        {
            Assert.That(service.GetGalgameSources(), Does.Contain(source));
            Assert.That(service.GetGalgameSource(GalgameSourceType.LocalFolder, path), Is.SameAs(source));
            // UiThreadInvokeHelper未初始化时内联执行，因此事件在测试中可被断言
            Assert.That(raised, Is.EqualTo(1));
        });
    }

    // 验证重复添加同类型、同路径的库会抛出PvnException，防止同一库被添加两次
    [Test]
    public async Task AddGalgameSourceAsync_DuplicatePath_ThrowsPvnException()
    {
        GalgameSourceCollectionService service = await CreateInitializedServiceAsync();
        string path = CreateDir("lib");
        await service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, path, tryGetGalgame: false);

        Assert.ThrowsAsync<PvnException>(() =>
            service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, path, tryGetGalgame: false));
    }

    // 验证库会真实持久化到LiteDB：用同一份设置/数据库新建service实例（模拟应用重启）后能把之前添加的库重新加载出来
    [Test]
    public async Task Sources_PersistedToLiteDB_CanReloadInNewInstance()
    {
        GalgameSourceCollectionService service = await CreateInitializedServiceAsync();
        string path = CreateDir("lib");
        await service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, path, tryGetGalgame: false);

        // 用同一份设置/数据库新建实例，模拟应用重启后的加载
        GalgameSourceCollectionService service2 = await CreateInitializedServiceAsync();

        GalgameSourceBase? reloaded = service2.GetGalgameSource(GalgameSourceType.LocalFolder, path);
        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded!.SourceType, Is.EqualTo(GalgameSourceType.LocalFolder));
    }

    // 验证安装条目的移入/移出：移入时双向挂接到game.Sources与source.Galgames，移出后双向解除，并通知IGalgameCollectionService保存该游戏
    [Test]
    public async Task MoveInThenMoveOut_AttachesAndDetachesSourceEntry()
    {
        GalgameSourceCollectionService service = await CreateInitializedServiceAsync();
        string path = CreateDir("lib");
        GalgameSourceBase source = await service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, path,
            tryGetGalgame: false);
        Galgame game = new();
        string gamePath = CreateDir("lib/game1");

        GalgameAndPath? entry = service.MoveInNoOperate(source, game, gamePath);
        Assert.That(entry, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(game.Sources, Does.Contain(source));
            Assert.That(source.Galgames, Does.Contain(entry));
        });

        await service.MoveOutNoOperate(entry!);
        Assert.Multiple(() =>
        {
            Assert.That(game.Sources, Is.Empty);
            Assert.That(source.Galgames, Does.Not.Contain(entry));
        });
        GalgameCollectionService.Verify(x => x.SaveGalgameAsync(game), Times.Once);
    }

    // 验证路径嵌套的两个库会被正确计算出父子归属：子库的ParentSource指向父库，父库的SubSources包含子库
    [Test]
    public async Task AddGalgameSourceAsync_NestedPaths_ComputesParentChildRelation()
    {
        GalgameSourceCollectionService service = await CreateInitializedServiceAsync();
        string parentPath = CreateDir("a");
        string childPath = CreateDir("a/b");

        GalgameSourceBase parentSource = await service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder,
            parentPath, tryGetGalgame: false);
        GalgameSourceBase childSource = await service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder,
            childPath, tryGetGalgame: false);

        Assert.Multiple(() =>
        {
            Assert.That(childSource.ParentSource, Is.SameAs(parentSource));
            Assert.That(parentSource.SubSources, Does.Contain(childSource));
        });
    }

    // 验证通过"type://path"形式的URL能解析出库类型与路径，并找回对应的库实例
    [Test]
    public async Task GetGalgameSourceFromUrl_LocalFolderUrl_ResolvesSource()
    {
        GalgameSourceCollectionService service = await CreateInitializedServiceAsync();
        string path = CreateDir("lib");
        GalgameSourceBase source = await service.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, path,
            tryGetGalgame: false);

        string url = GalgameSourceBase.CalcUrl(GalgameSourceType.LocalFolder, path);

        Assert.That(service.GetGalgameSourceFromUrl(url), Is.SameAs(source));
    }
}
