using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Services;
using LiteDB;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public class CategoryServiceTest : ServiceTestBase
{
    private static readonly Guid StatusNoneId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid StatusPlayedId = new("00000000-0000-0000-0000-000000000002");

    private IMessenger _bus = null!;

    [SetUp]
    public void CategoryServiceTestSetUp()
    {
        // 每个用例用独立的Messenger实例，避免跨用例串消息
        _bus = new WeakReferenceMessenger();
    }

    private CategoryService CreateService(FakeLocalSettingsService? settings = null) =>
        new(settings ?? Settings, GalgameCollectionService.Object, InfoService.Object, _bus, BgTaskService.Object);

    // 验证全新数据库首次初始化：自动创建游玩状态/开发商/引擎三个内置分类组，
    // 游玩状态组内含6个固定Guid的状态分类，且全部分组与状态分类都写入LiteDB
    [Test]
    public async Task Init_FreshDatabase_CreatesBuiltInGroupsAndStatusCategories()
    {
        CategoryService service = CreateService();

        ObservableCollection<CategoryGroup> groups = await service.GetCategoryGroupsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(groups.Count(g => g.Type == CategoryGroupType.Status), Is.EqualTo(1));
            Assert.That(groups.Count(g => g.Type == CategoryGroupType.Developer), Is.EqualTo(1));
            Assert.That(groups.Count(g => g.Type == CategoryGroupType.Engine), Is.EqualTo(1));
            Assert.That(service.StatusGroup.Categories.Select(c => c.Id), Is.EquivalentTo(new[]
            {
                new Guid("00000000-0000-0000-0000-000000000001"),
                new Guid("00000000-0000-0000-0000-000000000002"),
                new Guid("00000000-0000-0000-0000-000000000003"),
                new Guid("00000000-0000-0000-0000-000000000004"),
                new Guid("00000000-0000-0000-0000-000000000005"),
                new Guid("00000000-0000-0000-0000-000000000006"),
            }));
        });
        Assert.Multiple(() =>
        {
            Assert.That(Database.GetCollection<CategoryGroup>("category_group").FindAll().Count(),
                Is.EqualTo(3));
            Assert.That(Database.GetCollection<Category>("category").FindAll().Count(), Is.EqualTo(6));
        });
    }

    // 验证新增分类组：组加入内存列表并能按Id取回、写入LiteDB、通过IMessenger广播GroupAdded
    [Test]
    public async Task AddCategoryGroup_PersistsAndNotifies()
    {
        CategoryService service = CreateService();
        await service.Init();
        CategoryGroupChangedArg? received = null;
        _bus.Register<CategoryGroupChangedArg>(this, (_, m) => received = m);

        CategoryGroup group = service.AddCategoryGroup("我的分组");

        Assert.Multiple(() =>
        {
            Assert.That(service.GetGroup(group.Id), Is.SameAs(group));
            Assert.That(received?.ChangeType, Is.EqualTo(CategoryGroupChangeType.GroupAdded));
            Assert.That(received?.Group, Is.SameAs(group));
        });
        CategoryGroup? persisted = Database.GetCollection<CategoryGroup>("category_group")
            .FindById(group.Id);
        Assert.That(persisted?.Name, Is.EqualTo("我的分组"));
    }

    // 验证删除分类组：组被移除并广播GroupRemoved；只属该组的分类一并从内存与LiteDB删除，
    // 同时属于其他组的共享分类保留
    [Test]
    public async Task DeleteCategoryGroup_RemovesOrphanCategoryButKeepsShared()
    {
        CategoryService service = CreateService();
        await service.Init();
        CategoryGroup group1 = service.AddCategoryGroup("组一");
        CategoryGroup group2 = service.AddCategoryGroup("组二");
        Category shared = new("共享分类");
        Category orphan = new("独占分类");
        service.AddCategoryToGroup(group1, shared);
        service.AddCategoryToGroup(group2, shared);
        service.AddCategoryToGroup(group1, orphan);
        CategoryGroupChangedArg? received = null;
        _bus.Register<CategoryGroupChangedArg>(this, (_, m) => received = m);

        service.DeleteCategoryGroup(group1);

        Assert.Multiple(() =>
        {
            Assert.That(service.GetGroup(group1.Id), Is.Null);
            Assert.That(service.GetCategory(orphan.Id), Is.Null);
            Assert.That(service.GetCategory(shared.Id), Is.SameAs(shared));
            Assert.That(group2.Categories, Does.Contain(shared));
            Assert.That(received?.ChangeType, Is.EqualTo(CategoryGroupChangeType.GroupRemoved));
        });
        ILiteCollection<Category> categoryDb = Database.GetCollection<Category>("category");
        Assert.Multiple(() =>
        {
            Assert.That(categoryDb.FindById(orphan.Id), Is.Null);
            Assert.That(categoryDb.FindById(shared.Id), Is.Not.Null);
            Assert.That(Database.GetCollection<CategoryGroup>("category_group").FindById(group1.Id), Is.Null);
        });
    }

    // 验证向组添加/移除分类：组与分类的关联写入LiteDB（通过CategoryIds），
    // 重复添加是空操作，增删各自广播CategoryAdded/CategoryRemoved
    [Test]
    public async Task AddAndRemoveCategoryToGroup_PersistsRelationAndNotifies()
    {
        CategoryService service = CreateService();
        await service.Init();
        CategoryGroup group = service.AddCategoryGroup("组");
        Category category = new("分类");
        List<CategoryGroupChangeType> received = new();
        _bus.Register<CategoryGroupChangedArg>(this, (_, m) => received.Add(m.ChangeType));

        service.AddCategoryToGroup(group, category);
        service.AddCategoryToGroup(group, category); // 重复添加应直接返回

        Assert.Multiple(() =>
        {
            Assert.That(group.Categories, Does.Contain(category));
            Assert.That(received, Is.EqualTo(new[] { CategoryGroupChangeType.CategoryAdded }));
            Assert.That(Database.GetCollection<CategoryGroup>("category_group").FindById(group.Id)
                .GetLoadedCategoryIds(), Does.Contain(category.Id));
        });

        service.RemoveCategoryFromGroup(group, category);

        Assert.Multiple(() =>
        {
            Assert.That(group.Categories, Does.Not.Contain(category));
            Assert.That(received,
                Is.EqualTo(new[] { CategoryGroupChangeType.CategoryAdded, CategoryGroupChangeType.CategoryRemoved }));
            Assert.That(Database.GetCollection<CategoryGroup>("category_group").FindById(group.Id)
                .GetLoadedCategoryIds(), Is.Empty);
        });
    }

    // 验证删除分类：分类从所有引用它的组中移除、从LiteDB删除、可按Id/名字都查不到
    [Test]
    public async Task DeleteCategory_RemovesFromAllGroupsAndDb()
    {
        CategoryService service = CreateService();
        await service.Init();
        CategoryGroup group1 = service.AddCategoryGroup("组一");
        CategoryGroup group2 = service.AddCategoryGroup("组二");
        Category category = new("待删分类");
        service.AddCategoryToGroup(group1, category);
        service.AddCategoryToGroup(group2, category);

        service.DeleteCategory(category);

        Assert.Multiple(() =>
        {
            Assert.That(group1.Categories, Does.Not.Contain(category));
            Assert.That(group2.Categories, Does.Not.Contain(category));
            Assert.That(service.GetCategory(category.Id), Is.Null);
            Assert.That(service.GetCategory("待删分类"), Is.Null);
            Assert.That(Database.GetCollection<Category>("category").FindById(category.Id), Is.Null);
        });
    }

    // 验证合并分类：源分类的游戏并入目标分类，源分类从组与LiteDB中删除
    [Test]
    public async Task Merge_MovesGamesAndDeletesSource()
    {
        CategoryService service = CreateService();
        await service.Init();
        CategoryGroup group = service.AddCategoryGroup("组");
        Category target = new("目标分类");
        Category source = new("源分类");
        service.AddCategoryToGroup(group, target);
        service.AddCategoryToGroup(group, source);
        Galgame game = new("游戏");
        source.Add(game);
        service.Save(category: source);

        service.Merge(target, source);

        Assert.Multiple(() =>
        {
            Assert.That(target.GalgamesX, Does.Contain(game));
            Assert.That(game.Categories, Does.Contain(target));
            Assert.That(game.Categories, Does.Not.Contain(source));
            Assert.That(group.Categories, Does.Contain(target));
            Assert.That(group.Categories, Does.Not.Contain(source));
            Assert.That(Database.GetCollection<Category>("category").FindById(source.Id), Is.Null);
        });
    }

    // 验证开启自动分类后新增游戏：按开发商名在开发商组中自动创建分类并把游戏挂进去。
    // 注：新建开发商分类会尝试创建DownloadCategoryImageTask下载图片，该任务静态构造依赖App.GetService，
    // 在测试进程中必抛TypeInitializationException，由生产代码的try/catch吞掉（既有行为），不影响分类创建。
    // 开发商名用producers.json里不存在的虚构名，否则会被ProducerDataHelper规范化成别名（测试输出目录含该数据文件）
    [Test]
    public async Task GalgameMutation_Added_AttachesAndCreatesDeveloperCategory()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoCategory, true);
        Galgame game = new("测试游戏") { Developer = "虚构开发商甲" };
        ObservableCollection<Galgame> games = [];
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(games);
        CategoryService service = CreateService();
        await service.Init();

        games.Add(game);
        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Added, GalgameChangeOrigin.LocalOperation));

        await WaitUntilAsync(() => service.DeveloperGroup.Categories.Any(c => c.Name == "虚构开发商甲"),
            "等待自动创建开发商分类超时");
        Category developer = service.DeveloperGroup.Categories.First(c => c.Name == "虚构开发商甲");
        Assert.Multiple(() =>
        {
            Assert.That(developer.GalgamesX, Does.Contain(game));
            Assert.That(game.Categories, Does.Contain(developer));
            Assert.That(Database.GetCollection<Category>("category").FindById(developer.Id), Is.Not.Null);
        });
    }

    // 验证开启自动分类后修改游玩状态：游戏从旧状态分类移入新状态分类
    // （状态分类Guid尾号002对应Played，见CategoryService.InitStatusGroup的映射表）
    [Test]
    public async Task PlayTypeChanged_MovesGameBetweenStatusCategories()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoCategory, true);
        Galgame game = new("测试游戏"); // PlayType默认None
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();
        // 全新初始化会把没有状态分类的游戏放进当前状态对应的分类
        Category noneCategory = service.StatusGroup.Categories.First(c => c.Id == StatusNoneId);
        Category playedCategory = service.StatusGroup.Categories.First(c => c.Id == StatusPlayedId);
        Assert.That(noneCategory.GalgamesX, Does.Contain(game));

        game.PlayType = PlayType.Played;

        await WaitUntilAsync(() => playedCategory.GalgamesX.Contains(game), "等待状态分类移动超时");
        Assert.Multiple(() =>
        {
            Assert.That(noneCategory.GalgamesX, Does.Not.Contain(game));
            Assert.That(game.Categories, Does.Contain(playedCategory));
            Assert.That(game.Categories, Does.Not.Contain(noneCategory));
        });
    }

    // 验证删除游戏事件：游戏被移出其所属的所有分类
    [Test]
    public async Task GalgameDeleted_RemovesGameFromCategories()
    {
        Galgame game = new("测试游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();
        CategoryGroup group = service.AddCategoryGroup("组");
        Category category = new("分类");
        service.AddCategoryToGroup(group, category);
        category.Add(game);
        service.Save(category: category);
        Assert.That(game.Categories, Is.Not.Empty);

        GalgameCollectionService.Raise(x => x.GalgameDeletedEvent += null, game);

        Assert.Multiple(() =>
        {
            Assert.That(category.GalgamesX, Does.Not.Contain(game));
            Assert.That(game.Categories, Is.Empty);
        });
    }

    // 验证持久化闭环：第一个实例写入的自定义组/分类/游戏关联，
    // 在模拟重启（重建LiteDB连接与新实例）后能完整恢复，游戏通过GetGalgameFromUuid重新挂接
    [Test]
    public async Task Init_SecondInstance_RestoresGroupsAndGameRelations()
    {
        Galgame game = new("测试游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        GalgameCollectionService.Setup(x => x.GetGalgameFromUuid(It.IsAny<Guid?>()))
            .Returns((Guid? id) => id == game.Uuid ? game : null);
        CategoryService first = CreateService();
        await first.Init();
        CategoryGroup group = first.AddCategoryGroup("组一");
        Category category = new("分类一");
        first.AddCategoryToGroup(group, category);
        category.Add(game);
        first.Save(category: category);

        // 模拟应用重启：关掉旧连接（TearDown会dispose这里换上的新连接），用新设置实例再Init
        Database.Dispose();
        Database = new LiteDatabase(Path.Combine(TestDir, "data.db"));
        FakeLocalSettingsService settings2 = new(Database, TestDir);
        CategoryService second = CreateService(settings2);
        await second.Init();

        CategoryGroup? restoredGroup = second.GetGroup(group.Id);
        Category? restoredCategory = second.GetCategory(category.Id);
        Assert.Multiple(() =>
        {
            Assert.That(restoredGroup?.Name, Is.EqualTo("组一"));
            Assert.That(restoredCategory, Is.Not.Null);
            Assert.That(restoredGroup!.Categories, Does.Contain(restoredCategory));
            Assert.That(restoredCategory!.GalgamesX, Has.Count.EqualTo(1));
            Assert.That(restoredCategory.GalgamesX[0], Is.SameAs(game));
        });
    }

    // 验证导出：分类组深拷贝后连同分类一起写入导出存储（FakeLocalSettingsService.Exported）
    [Test]
    public async Task ExportAsync_WritesCategoryGroupsToExport()
    {
        CategoryService service = CreateService();
        await service.Init();
        CategoryGroup group = service.AddCategoryGroup("导出组");
        Category category = new("导出分类");
        service.AddCategoryToGroup(group, category);

        await service.ExportAsync(null);

        Assert.That(Settings.Exported, Does.ContainKey(KeyValues.CategoryGroups));
        ObservableCollection<CategoryGroup> exported =
            (ObservableCollection<CategoryGroup>)Settings.Exported[KeyValues.CategoryGroups];
        CategoryGroup? exportedGroup = exported.FirstOrDefault(g => g.Name == "导出组");
        Assert.Multiple(() =>
        {
            Assert.That(exportedGroup, Is.Not.Null);
            Assert.That(exportedGroup!.Categories.Select(c => c.Id), Does.Contain(category.Id));
        });
    }

    // 验证手动编辑开发商字段（LockableProperty.Value）：游戏挂入新开发商分类；
    // 再次修改后从旧分类移除、挂入新分类
    [Test]
    public async Task DeveloperEdited_MovesGameToNewDeveloperCategory()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoCategory, true);
        Galgame game = new("测试游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();

        game.Developer.Value = "虚构开发商甲";
        await WaitUntilAsync(() => service.DeveloperGroup.Categories.Any(c => c.Name == "虚构开发商甲"),
            "等待开发商分类创建超时");

        game.Developer.Value = "虚构开发商乙";
        await WaitUntilAsync(() => service.DeveloperGroup.Categories.Any(c => c.Name == "虚构开发商乙"),
            "等待开发商分类移动超时");
        Category oldCategory = service.DeveloperGroup.Categories.First(c => c.Name == "虚构开发商甲");
        Category newCategory = service.DeveloperGroup.Categories.First(c => c.Name == "虚构开发商乙");
        Assert.Multiple(() =>
        {
            Assert.That(newCategory.GalgamesX, Does.Contain(game));
            Assert.That(oldCategory.GalgamesX, Does.Not.Contain(game));
            Assert.That(game.Categories, Does.Not.Contain(oldCategory));
        });
    }

    // 验证开发商字段含逗号分割的多个开发商时，每个开发商各建一个分类且都挂上游戏
    [Test]
    public async Task DeveloperEdited_MultipleDevelopers_CreatesCategoryForEach()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoCategory, true);
        Galgame game = new("测试游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();

        game.Developer.Value = "虚构开发商甲,虚构开发商乙";

        await WaitUntilAsync(() =>
                service.DeveloperGroup.Categories.Any(c => c.Name == "虚构开发商甲") &&
                service.DeveloperGroup.Categories.Any(c => c.Name == "虚构开发商乙"),
            "等待多开发商分类创建超时");
        Assert.Multiple(() =>
        {
            Assert.That(service.DeveloperGroup.Categories.First(c => c.Name == "虚构开发商甲").GalgamesX,
                Does.Contain(game));
            Assert.That(service.DeveloperGroup.Categories.First(c => c.Name == "虚构开发商乙").GalgamesX,
                Does.Contain(game));
        });
    }

    // 验证手动编辑引擎字段：游戏挂入新引擎分类；再次修改后从旧分类移除、挂入新分类
    [Test]
    public async Task EngineEdited_MovesGameToNewEngineCategory()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoCategory, true);
        Galgame game = new("测试游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();

        game.Engine.Value = "Unity";
        await WaitUntilAsync(() => service.EngineGroup.Categories.Any(c => c.Name == "Unity"),
            "等待引擎分类创建超时");

        game.Engine.Value = "KiriKiri";
        await WaitUntilAsync(() => service.EngineGroup.Categories.Any(c => c.Name == "KiriKiri"),
            "等待引擎分类移动超时");
        Assert.Multiple(() =>
        {
            Assert.That(service.EngineGroup.Categories.First(c => c.Name == "KiriKiri").GalgamesX,
                Does.Contain(game));
            Assert.That(service.EngineGroup.Categories.First(c => c.Name == "Unity").GalgamesX,
                Does.Not.Contain(game));
        });
    }

    // 验证修改最后游玩时间：游戏所属所有分类的LastPlayed同步更新为最新游玩时间
    // （LastPlayTime分支是同步执行的，无需轮询；也不受AutoCategory开关控制）
    [Test]
    public async Task LastPlayTimeChanged_UpdatesCategoryLastPlayed()
    {
        Galgame game = new("测试游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();
        // 全新初始化已把游戏放进None状态分类；再手动挂一个自定义分类
        Category noneCategory = service.StatusGroup.Categories.First(c => c.Id == StatusNoneId);
        CategoryGroup group = service.AddCategoryGroup("组");
        Category category = new("分类");
        service.AddCategoryToGroup(group, category);
        category.Add(game);
        Assert.That(noneCategory.GalgamesX, Does.Contain(game));

        DateTime playTime = new(2024, 5, 1);
        game.LastPlayTime = playTime;

        Assert.Multiple(() =>
        {
            Assert.That(noneCategory.LastPlayed, Is.EqualTo(playTime));
            Assert.That(category.LastPlayed, Is.EqualTo(playTime));
        });
    }

    // 验证关闭自动分类（未设置AutoCategory，默认false）时，编辑开发商字段不会创建任何分类
    [Test]
    public async Task DeveloperEdited_AutoCategoryDisabled_DoesNotCategorize()
    {
        Galgame game = new("测试游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();

        game.Developer.Value = "虚构开发商甲";
        await Task.Delay(500); // 负向断言：留给fire-and-forget足够的时间窗，证明确实不会发生

        Assert.That(service.DeveloperGroup.Categories, Is.Empty);
    }

    // 验证统一变更事件的Metadata通知：对已存在于库中、
    // 初始化时未分类的游戏按当前开发商/引擎补充分类
    [Test]
    public async Task GalgameMutation_Metadata_CategorizesExistingGame()
    {
        await Settings.SaveSettingAsync(KeyValues.AutoCategory, true);
        Galgame game = new("测试游戏") { Developer = "虚构开发商甲", Engine = "Unity" };
        GalgameCollectionService.SetupGet(x => x.Galgames)
            .Returns(new ObservableCollection<Galgame> { game });
        CategoryService service = CreateService();
        await service.Init();
        // 初始化不会主动给已有游戏做开发商/引擎分类
        Assert.That(service.DeveloperGroup.Categories, Is.Empty);

        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Metadata, GalgameChangeOrigin.PvnSync));

        await WaitUntilAsync(() =>
                service.DeveloperGroup.Categories.Any(c => c.Name == "虚构开发商甲") &&
                service.EngineGroup.Categories.Any(c => c.Name == "Unity"),
            "等待Changed事件触发的分类超时");
        Assert.Multiple(() =>
        {
            Assert.That(service.DeveloperGroup.Categories.First(c => c.Name == "虚构开发商甲").GalgamesX,
                Does.Contain(game));
            Assert.That(service.EngineGroup.Categories.First(c => c.Name == "Unity").GalgamesX,
                Does.Contain(game));
        });
    }
}
