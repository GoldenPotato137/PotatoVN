using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.WinApp.Base.Contracts;
using LiteDB;
using Moq;

namespace GalgameManager.Test.Services;

/// <summary>
/// GalgameCollectionService单元测试。构造函数内new的各Phraser在测试进程可安全构造（纯数据对象），
/// 测试时用mock替换PhraserList槽位以隔离网络。<br/>
/// AddGameAsync链路里的后台任务通过IBgTaskService.CreateBgTask工厂创建（构造函数注入依赖），
/// 测试setup工厂返回真实任务实例——因AddBgTask被mock不会真正Run，任务仅入队不执行。
/// </summary>
[TestFixture]
public class GalgameCollectionServiceTest : ServiceTestBase
{
    private Mock<IJumpListService> _jumpListService = null!;
    private Mock<IGalgameSourceCollectionService> _galSrcService = null!;
    private IMessenger _bus = null!;

    [SetUp]
    public void SetUp()
    {
        _jumpListService = new Mock<IJumpListService>();
        _jumpListService.Setup(x => x.CheckJumpListAsync(It.IsAny<IList<Galgame>>()))
            .Returns(Task.CompletedTask);
        _galSrcService = new Mock<IGalgameSourceCollectionService>();
        _galSrcService.Setup(x => x.MoveOutNoOperate(It.IsAny<GalgameAndPath>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        _bus = new WeakReferenceMessenger();
    }

    private GalgameCollectionService CreateService() => new(Settings, _jumpListService.Object,
        _galSrcService.Object, InfoService.Object, BgTaskService.Object, _bus);

    private async Task<GalgameCollectionService> CreateInitializedServiceAsync()
    {
        GalgameCollectionService service = CreateService();
        await service.InitAsync();
        return service;
    }

    private ILiteCollection<Galgame> DbSet => Database.GetCollection<Galgame>("galgame");

    private static Galgame CreateGame(string name)
    {
        Galgame game = new();
        game.Name.Value = name;
        return game;
    }

    /// 把mock的搜刮器塞进指定RssType槽位，替换掉构造函数内建的真实搜刮器
    private static Mock<IGalInfoPhraser> SetupPhraser(GalgameCollectionService service, RssType slot,
        Galgame? parseResult)
    {
        Mock<IGalInfoPhraser> phraser = new();
        phraser.Setup(x => x.GetGalgameInfo(It.IsAny<Galgame>())).ReturnsAsync(parseResult);
        phraser.Setup(x => x.GetPhraseType()).Returns(slot);
        service.PhraserList[(int)slot] = phraser.Object;
        return phraser;
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task PhraseGalCharacterAsync_SameNameInDifferentGames_DoesNotOverwriteOtherGameImage(bool provideGameUuid)
    {
        GalgameCollectionService service = CreateService();
        Galgame gameA = CreateGame("游戏A");
        Galgame gameB = CreateGame("游戏B");
        GalgameCharacter characterA = new() { Name = "同名角色" };
        GalgameCharacter characterB = new() { Name = "同名角色" };
        gameA.Characters.Add(characterA);
        gameB.Characters.Add(characterB);
        service.Galgames.Add(gameA);
        service.Galgames.Add(gameB);

        using HttpClient client = new(new CharacterImageHandler());
        Mock<IGalInfoPhraser> phraser = new();
        phraser.As<IHttpClientProvider>().SetupGet(p => p.HttpClient).Returns(client);
        phraser.As<IGalCharacterPhraser>().Setup(p => p.GetGalgameCharacter(It.IsAny<GalgameCharacter>()))
            .ReturnsAsync(() => new GalgameCharacter { Name = "同名角色", ImageUrl = "https://example.invalid/image.png" });
        service.PhraserList[(int)RssType.Bangumi] = phraser.Object;

        await service.PhraseGalCharacterAsync(characterA, RssType.Bangumi, provideGameUuid ? gameA.Uuid : null);
        await service.PhraseGalCharacterAsync(characterB, RssType.Bangumi, provideGameUuid ? gameB.Uuid : null);
        Assert.That(File.Exists(characterA.ImagePath), Is.True);
        Assert.That(File.Exists(characterB.ImagePath), Is.True);
        byte[] originalA = await File.ReadAllBytesAsync(characterA.ImagePath);
        byte[] originalB = await File.ReadAllBytesAsync(characterB.ImagePath);

        await service.PhraseGalCharacterAsync(characterA, RssType.Bangumi, provideGameUuid ? gameA.Uuid : null);

        Assert.Multiple(() =>
        {
            Assert.That(characterA.ImagePath, Is.Not.EqualTo(characterB.ImagePath));
            Assert.That(File.ReadAllBytes(characterA.ImagePath), Is.Not.EqualTo(originalA));
            Assert.That(File.ReadAllBytes(characterB.ImagePath), Is.EqualTo(originalB));
        });
    }

    private sealed class CharacterImageHandler : HttpMessageHandler
    {
        private byte _downloadCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // 下载器按文件头识别格式；每次返回不同内容，验证覆盖范围，不访问网络。
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, ++_downloadCount]),
            });
        }
    }

    #region 初始化与加载

    // 验证空数据库初始化：游戏列表为空、触发GalgameLoadedEvent、检查JumpList
    [Test]
    public async Task InitAsync_EmptyDatabase_LoadsEmptyListAndFiresLoadedEvent()
    {
        GalgameCollectionService service = CreateService();
        var loadedFired = false;
        service.GalgameLoadedEvent += () => loadedFired = true;

        await service.InitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.Galgames, Is.Empty);
            Assert.That(loadedFired, Is.True);
        });
        _jumpListService.Verify(x => x.CheckJumpListAsync(It.IsAny<IList<Galgame>>()), Times.Once);
    }

    // 验证从LiteDB恢复游戏列表：库中预存的游戏在InitAsync后出现在内存列表中
    [Test]
    public async Task InitAsync_GamesInDatabase_RestoresGamesIntoList()
    {
        // 预置LiteDB升级完成标记并跳过两项数据升级（FindSaveInPath依赖真实系统文件夹，测试进程不可用）
        await Settings.SaveSettingAsync(KeyValues.DataStatus, new LocalSettingStatus { GameLiteDbUpgrade = true }, true);
        await Settings.SaveSettingAsync(KeyValues.IdFromMixedUpgraded, true);
        await Settings.SaveSettingAsync(KeyValues.SavePathUpgraded, true);
        DbSet.Upsert(CreateGame("游戏甲"));
        DbSet.Upsert(CreateGame("游戏乙"));

        GalgameCollectionService service = CreateService();
        await service.InitAsync();

        Assert.That(service.Galgames.Select(g => g.Name.Value),
            Is.EquivalentTo(new[] { "游戏甲", "游戏乙" }));
    }

    // 验证旧版数据兼容：Ids数组长度不足PhraserNumber的游戏在加载时会被扩容
    [Test]
    public async Task InitAsync_LegacyGameWithShortIdsArray_ExpandsIdsToPhraserNumber()
    {
        await Settings.SaveSettingAsync(KeyValues.DataStatus, new LocalSettingStatus { GameLiteDbUpgrade = true }, true);
        await Settings.SaveSettingAsync(KeyValues.IdFromMixedUpgraded, true);
        await Settings.SaveSettingAsync(KeyValues.SavePathUpgraded, true);
        Galgame legacy = CreateGame("旧版游戏");
        legacy.Ids = new string?[3];
        DbSet.Upsert(legacy);

        GalgameCollectionService service = CreateService();
        await service.InitAsync();

        Galgame loaded = service.Galgames.Single(g => g.Name.Value == "旧版游戏");
        Assert.That(loaded.Ids.Length, Is.EqualTo(Galgame.PhraserNumber));
    }

    #endregion

    #region 添加游戏（虚拟游戏）

    // 验证添加虚拟游戏：加入内存列表、持久化并在完成后触发一次Added变更
    [Test]
    public async Task AddVirtualGalgame_AddsToListFiresEventAndPersists()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        List<GalgameMutationEventArgs> mutations = [];
        service.GalgameMutated += (_, args) => mutations.Add(args);
        Galgame game = CreateGame("虚拟游戏");

        await service.AddVirtualGalgameAsync(game);

        Assert.Multiple(() =>
        {
            Assert.That(service.Galgames, Does.Contain(game));
            Assert.That(mutations, Has.Count.EqualTo(1));
            Assert.That(mutations[0].Game, Is.SameAs(game));
            Assert.That(mutations[0].Changes, Is.EqualTo(GalgameChangeKind.Added));
            Assert.That(mutations[0].Origin, Is.EqualTo(GalgameChangeOrigin.LocalOperation));
            Assert.That(mutations[0].ParsedTypes, Is.EqualTo(GameParseType.None));
        });
        Assert.That(DbSet.FindById(game.Uuid), Is.Not.Null);
    }

    [Test]
    public async Task GalgameMutated_FailingSubscriber_DoesNotBlockOtherSubscribers()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        var secondSubscriberCalled = false;
        service.GalgameMutated += (_, _) => throw new InvalidOperationException("监听者异常");
        service.GalgameMutated += (_, _) => secondSubscriberCalled = true;

        Assert.DoesNotThrowAsync(async () => await service.AddVirtualGalgameAsync(CreateGame("虚拟游戏")));

        Assert.That(secondSubscriberCalled, Is.True);
    }

    // 验证添加游戏主流程（虚拟库）：搜刮信息合并进新游戏、加入列表并移入对应的库、
    // 在所有搜刮、来源关联和持久化完成后只触发一次统一变更事件
    [Test]
    public async Task AddGameAsync_VirtualSource_AddsParsedGameToListAndDatabase()
    {
        // 信息源设置为Bangumi并用mock搜刮器替换槽位，避免网络访问
        await Settings.SaveSettingAsync(KeyValues.RssType, RssType.Bangumi);
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame parsed = new();
        parsed.RssType = RssType.Bangumi;
        parsed.Id = "bgm-456";
        parsed.CnName = "搜刮到的中文名";
        parsed.Name.Value = "Parsed Original Name";
        SetupPhraser(service, RssType.Bangumi, parsed);
        // 后台任务工厂返回真实任务实例（构造依赖真实service与mock的PvnService）；
        // AddBgTask是mock不会真正Run，任务实例只入队不执行，不会触发网络下载
        BgTaskService.Setup(x => x.CreateBgTask<GetHeaderFromRssTask>(It.IsAny<object[]>()))
            .Returns(() => new GetHeaderFromRssTask(service, new Mock<IPvnService>().Object, Settings));
        GalgameSourceBase source = new Mock<GalgameSourceBase>().Object;
        _galSrcService.Setup(x => x.GetSourcePath(GalgameSourceType.Virtual, "游戏甲")).Returns("游戏甲");
        _galSrcService.Setup(x => x.GetGalgameSource(GalgameSourceType.Virtual, "游戏甲"))
            .Returns((GalgameSourceBase?)null);
        _galSrcService.Setup(x => x.AddGalgameSourceAsync(GalgameSourceType.Virtual, "游戏甲",
                It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(source);
        List<GalgameMutationEventArgs> mutations = [];
        service.GalgameMutated += (_, args) => mutations.Add(args);

        Galgame result = await service.AddGameAsync(GalgameSourceType.Virtual, "游戏甲", force: true,
            requireConfirm: false);

        Assert.Multiple(() =>
        {
            Assert.That(service.Galgames, Does.Contain(result));
            // 默认显示名配置为中文名：Name被替换为搜刮到的中文名
            Assert.That(result.Name.Value, Is.EqualTo("搜刮到的中文名"));
            Assert.That(result.RssType, Is.EqualTo(RssType.Bangumi));
            Assert.That(result.Id, Is.EqualTo("bgm-456"));
            Assert.That(result.AddTime, Is.Not.EqualTo(DateTime.MinValue));
            Assert.That(mutations, Has.Count.EqualTo(1));
            Assert.That(mutations[0].Game, Is.SameAs(result));
            Assert.That(mutations[0].Changes.HasFlag(GalgameChangeKind.Added), Is.True);
            Assert.That(mutations[0].Changes.HasFlag(GalgameChangeKind.Metadata), Is.True);
            Assert.That(mutations[0].Changes.HasFlag(GalgameChangeKind.SourceEntries), Is.True);
            Assert.That(mutations[0].Origin, Is.EqualTo(GalgameChangeOrigin.LocalOperation));
            Assert.That(mutations[0].ParsedTypes, Is.EqualTo(GameParseType.None));
            Assert.That(DbSet.FindById(result.Uuid), Is.Not.Null);
        });
        _galSrcService.Verify(x => x.MoveInNoOperate(source, result, "游戏甲", null), Times.Once);
        BgTaskService.Verify(x => x.CreateBgTask<GetHeaderFromRssTask>(It.IsAny<object[]>()), Times.Once);
    }

    #endregion

    #region 路径取游戏名

    // 验证虚拟源取游戏名：直接返回传入路径本身作为游戏名
    [Test]
    public async Task GetNameFromPath_VirtualSource_ReturnsPathAsName()
    {
        GalgameCollectionService service = CreateService();

        string name = await service.GetNameFromPath(GalgameSourceType.Virtual, "随便一个名字");

        Assert.That(name, Is.EqualTo("随便一个名字"));
    }

    // 验证本地文件夹源取游戏名：默认正则下取游戏可执行文件所在文件夹的名字
    [Test]
    public async Task GetNameFromPath_LocalFolder_DefaultPattern_ReturnsFolderName()
    {
        await Settings.SaveSettingAsync(KeyValues.RegexPattern, ".+");
        GalgameCollectionService service = CreateService();

        string name = await service.GetNameFromPath(GalgameSourceType.LocalFolder,
            Path.Combine("D:", "Games", "MyGame"));

        Assert.That(name, Is.EqualTo("MyGame"));
    }

    #endregion

    #region 删除游戏

    // 验证删除游戏：从内存列表移除、从LiteDB删除并触发GalgameDeletedEvent
    [Test]
    public async Task RemoveGalgame_RemovesFromListDatabaseAndFiresEvent()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("待删除游戏");
        await service.AddVirtualGalgameAsync(game);
        await WaitUntilAsync(() => DbSet.FindById(game.Uuid) is not null, "游戏未及时写入数据库");
        Galgame? deleted = null;
        service.GalgameDeletedEvent += g => deleted = g;

        await service.RemoveGalgame(game);

        Assert.Multiple(() =>
        {
            Assert.That(service.Galgames, Does.Not.Contain(game));
            Assert.That(deleted, Is.SameAs(game));
            Assert.That(DbSet.FindById(game.Uuid), Is.Null);
        });
    }

    // 验证删除不在列表中的游戏：直接返回，不触发任何事件也不报错
    [Test]
    public async Task RemoveGalgame_GameNotInList_DoesNothing()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        var eventFired = false;
        service.GalgameDeletedEvent += _ => eventFired = true;

        await service.RemoveGalgame(CreateGame("不在列表的游戏"));

        Assert.That(eventFired, Is.False);
    }

    [Test]
    public async Task SaveGalgameAsync_RemovedInstance_DoesNotReinsertDeletedGame()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("待删除后保存的游戏");
        await service.AddVirtualGalgameAsync(game);

        await service.RemoveGalgame(game);
        game.Comment = "后台任务的迟到修改";
        await service.SaveGalgameAsync(game);

        Assert.Multiple(() =>
        {
            Assert.That(service.Galgames, Does.Not.Contain(game));
            Assert.That(DbSet.FindById(game.Uuid), Is.Null);
        });
    }

    [Test]
    public async Task SaveGalgameAsync_OldInstance_DoesNotOverwriteReaddedGameWithSameUuid()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame oldGame = CreateGame("旧游戏对象");
        await service.AddVirtualGalgameAsync(oldGame);
        await service.RemoveGalgame(oldGame);

        Galgame readdedGame = CreateGame("重新添加的游戏");
        readdedGame.Uuid = oldGame.Uuid;
        await service.AddVirtualGalgameAsync(readdedGame);

        oldGame.Name.Value = "旧后台任务写回的名称";
        await service.SaveGalgameAsync(oldGame);

        Galgame? loaded = DbSet.FindById(readdedGame.Uuid);
        Assert.Multiple(() =>
        {
            Assert.That(service.Galgames, Does.Contain(readdedGame));
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name.Value, Is.EqualTo("重新添加的游戏"));
        });
    }

    #endregion

    #region 游戏查询

    // 验证按Uuid查询：存在的Uuid返回对应游戏，不存在的返回null
    [Test]
    public async Task GetGalgameFromUuid_ReturnsGameOrNull()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("游戏甲");
        await service.AddVirtualGalgameAsync(game);

        Assert.Multiple(() =>
        {
            Assert.That(service.GetGalgameFromUuid(game.Uuid), Is.SameAs(game));
            Assert.That(service.GetGalgameFromUuid(Guid.NewGuid()), Is.Null);
            Assert.That(service.GetGalgameFromUuid(null), Is.Null);
        });
    }

    // 验证按信息源Id查询：只匹配对应RssType槽位的Id，其他槽位同名字段不影响结果
    [Test]
    public async Task GetGalgameFromId_MatchesOnlyCorrespondingRssSlot()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("游戏甲");
        game.Ids[(int)RssType.Bangumi] = "12345";
        await service.AddVirtualGalgameAsync(game);

        Assert.Multiple(() =>
        {
            Assert.That(service.GetGalgameFromId("12345", RssType.Bangumi), Is.SameAs(game));
            Assert.That(service.GetGalgameFromId("12345", RssType.Vndb), Is.Null);
            Assert.That(service.GetGalgameFromId(null, RssType.Bangumi), Is.Null);
        });
    }

    // 验证按名称查询：精确匹配游戏名，空串与不存在的名字返回null
    [Test]
    public async Task GetGalgameFromName_MatchesExactNameOrReturnsNull()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("游戏甲");
        await service.AddVirtualGalgameAsync(game);

        Assert.Multiple(() =>
        {
            Assert.That(service.GetGalgameFromName("游戏甲"), Is.SameAs(game));
            Assert.That(service.GetGalgameFromName("不存在的游戏"), Is.Null);
            Assert.That(service.GetGalgameFromName(string.Empty), Is.Null);
            Assert.That(service.GetGalgameFromName(null), Is.Null);
        });
    }

    // 验证按Uid查询：Same模式匹配同一游戏，MaxSimilarity模式返回相似度最高的游戏
    [Test]
    public async Task GetGalgameFromUid_SameAndMaxSimilarityModes()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame gameA = CreateGame("白色相簿2");
        Galgame gameB = CreateGame("千恋万花");
        await service.AddVirtualGalgameAsync(gameA);
        await service.AddVirtualGalgameAsync(gameB);

        Assert.Multiple(() =>
        {
            Assert.That(service.GetGalgameFromUid(gameA.Uid), Is.SameAs(gameA));
            Assert.That(service.GetGalgameFromUid(gameA.Uid, GalgameUidFetchMode.MaxSimilarity),
                Is.SameAs(gameA));
            Assert.That(service.GetGalgameFromUid(null), Is.Null);
        });
    }

    #endregion

    #region 搜索建议

    // 验证搜索建议的来源：游戏名、开发商、Tag、中文名与原名都会产生建议
    [Test]
    public async Task GetSearchSuggestions_MatchesNameDeveloperTagsAndAliases()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("白色相簿2");
        game.Developer.Value = "Leaf社";
        game.Tags.Value = new ObservableCollection<string> { "恋爱", "胃痛" };
        game.ChineseName.Value = "白色相簿2";
        game.OriginalName.Value = "WHITE ALBUM 2";
        await service.AddVirtualGalgameAsync(game);

        List<string> byName = await service.GetSearchSuggestions("白色");
        List<string> byDeveloper = await service.GetSearchSuggestions("Leaf");
        List<string> byTag = await service.GetSearchSuggestions("胃痛");
        List<string> byOriginalName = await service.GetSearchSuggestions("ALBUM");

        Assert.Multiple(() =>
        {
            Assert.That(byName, Does.Contain("白色相簿2"));
            Assert.That(byDeveloper, Does.Contain("Leaf社"));
            Assert.That(byTag, Does.Contain("胃痛"));
            Assert.That(byOriginalName, Does.Contain("WHITE ALBUM 2"));
        });
    }

    // 验证搜索建议的开关：所有来源开关都关闭时不产生任何建议
    [Test]
    public async Task GetSearchSuggestions_AllSwitchesOff_ReturnsEmpty()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("白色相簿2");
        game.Developer.Value = "Leaf社";
        game.Tags.Value = new ObservableCollection<string> { "恋爱" };
        await service.AddVirtualGalgameAsync(game);

        List<string> result = await service.GetSearchSuggestions("白", false, false, false, false, false);
        List<string> tagOff = await service.GetSearchSuggestions("恋爱", searchTag: false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(tagOff, Is.Empty);
        });
    }

    // 验证搜索建议去重：多个游戏拥有相同Tag时，该Tag在建议中只出现一次
    [Test]
    public async Task GetSearchSuggestions_DeduplicatesResults()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame gameA = CreateGame("游戏甲");
        gameA.Tags.Value = new ObservableCollection<string> { "恋爱" };
        Galgame gameB = CreateGame("游戏乙");
        gameB.Tags.Value = new ObservableCollection<string> { "恋爱" };
        await service.AddVirtualGalgameAsync(gameA);
        await service.AddVirtualGalgameAsync(gameB);

        List<string> result = await service.GetSearchSuggestions("恋爱");

        Assert.That(result.Count(s => s == "恋爱"), Is.EqualTo(1));
    }

    #endregion

    #region 保存与元数据

    // 验证保存单个游戏：写入LiteDB且字段可完整往返
    [Test]
    public async Task SaveGalgameAsync_UpsertsGameToDatabase()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("待保存游戏");
        game.Developer.Value = "某开发商";

        await service.SaveGalgameAsync(game);

        Galgame? loaded = DbSet.FindById(game.Uuid);
        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name.Value, Is.EqualTo("待保存游戏"));
            Assert.That(loaded.Developer.Value, Is.EqualTo("某开发商"));
        });
    }

    // 验证保存整个列表：内存列表中的所有游戏都写入LiteDB
    [Test]
    public async Task SaveGalgamesAsync_PersistsWholeList()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame gameA = CreateGame("游戏甲");
        Galgame gameB = CreateGame("游戏乙");
        await service.AddVirtualGalgameAsync(gameA);
        await service.AddVirtualGalgameAsync(gameB);

        await service.SaveGalgamesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(DbSet.FindById(gameA.Uuid), Is.Not.Null);
            Assert.That(DbSet.FindById(gameB.Uuid), Is.Not.Null);
        });
    }

    // 验证保存元数据到指定库：库不包含该游戏时抛PvnException；目标库为null时不报错
    [Test]
    public async Task SaveGalgameMetaAsync_SourceNotContainingGame_Throws()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame game = CreateGame("游戏甲");
        // mock的抽象库：Galgames为空列表，Contain必然返回false
        Mock<GalgameSourceBase> source = new();

        Assert.DoesNotThrowAsync(async () => await service.SaveGalgameMetaAsync(game));
        Assert.ThrowsAsync<PvnException>(async () =>
            await service.SaveGalgameMetaAsync(game, source.Object));
    }

    #endregion

    #region 信息解析

    // 验证搜刮结果合并：搜刮器返回的各字段合并进游戏，RssType/Id/搜刮时间同步更新
    [Test]
    public async Task ParseGalInfoOnlyAsync_GameInfo_MergesPhrasedFields()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame parsed = new();
        parsed.RssType = RssType.Bangumi;
        parsed.Id = "bgm-123";
        parsed.Description.Value = "新简介";
        parsed.Developer.Value = "新开发商";
        parsed.Engine.Value = "新引擎";
        parsed.ExpectedPlayTime.Value = "约20小时";
        parsed.CnName = "中文名";
        parsed.Name.Value = "Original Name";
        parsed.Rating.Value = 8.7f;
        parsed.Tags.Value = new ObservableCollection<string> { "剧情", "神作" };
        parsed.ReleaseDate.Value = new DateTime(2012, 12, 24);
        SetupPhraser(service, RssType.Bangumi, parsed);
        Galgame game = CreateGame("旧名字");

        await service.ParseGalInfoOnlyAsync(game, RssType.Bangumi);

        Assert.Multiple(() =>
        {
            // 默认显示名配置为中文名：Name被替换为中文名，原名进入OriginalName
            Assert.That(game.Name.Value, Is.EqualTo("中文名"));
            Assert.That(game.ChineseName.Value, Is.EqualTo("中文名"));
            Assert.That(game.OriginalName.Value, Is.EqualTo("Original Name"));
            Assert.That(game.Description.Value, Is.EqualTo("新简介"));
            Assert.That(game.Developer.Value, Is.EqualTo("新开发商"));
            Assert.That(game.Engine.Value, Is.EqualTo("新引擎"));
            Assert.That(game.ExpectedPlayTime.Value, Is.EqualTo("约20小时"));
            Assert.That(game.Rating.Value, Is.EqualTo(8.7f));
            Assert.That(game.Tags.Value, Is.EquivalentTo(new[] { "剧情", "神作" }));
            Assert.That(game.ReleaseDate.Value, Is.EqualTo(new DateTime(2012, 12, 24)));
            Assert.That(game.RssType, Is.EqualTo(RssType.Bangumi));
            Assert.That(game.Id, Is.EqualTo("bgm-123"));
            Assert.That(game.LastFetchInfoTime, Is.Not.EqualTo(DateTime.MinValue));
        });
    }

    // 验证搜刮器找不到信息（返回null）：游戏各字段保持原值不变
    [Test]
    public async Task ParseGalInfoOnlyAsync_PhraserReturnsNull_KeepsOriginalGame()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        SetupPhraser(service, RssType.Bangumi, null);
        Galgame game = CreateGame("原名字");
        string? originalDescription = game.Description.Value;

        await service.ParseGalInfoOnlyAsync(game, RssType.Bangumi);

        Assert.Multiple(() =>
        {
            Assert.That(game.Name.Value, Is.EqualTo("原名字"));
            Assert.That(game.Description.Value, Is.EqualTo(originalDescription));
        });
    }

    // 验证解析不在列表中的游戏：直接抛PvnException
    [Test]
    public async Task ParseGalInfoAsync_GameNotInList_Throws()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        SetupPhraser(service, RssType.Bangumi, new Galgame());

        Assert.ThrowsAsync<PvnException>(async () => await service.ParseGalInfoAsync(
            CreateGame("幽灵游戏"), RssType.Bangumi, requireConfirm: false, type: GameParseType.GameInfo));
    }

    // 验证解析列表中的游戏（仅基本信息）：字段合并、持久化并触发一次Metadata变更
    [Test]
    public async Task ParseGalInfoAsync_GameInfo_UpdatesGameFiresEventsAndPersists()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame parsed = new();
        parsed.RssType = RssType.Bangumi;
        parsed.Developer.Value = "新开发商";
        SetupPhraser(service, RssType.Bangumi, parsed);
        Galgame game = CreateGame("游戏甲");
        await service.AddVirtualGalgameAsync(game);
        List<GalgameMutationEventArgs> mutations = [];
        service.GalgameMutated += (_, args) => mutations.Add(args);

        await service.ParseGalInfoAsync(game, RssType.Bangumi, requireConfirm: false,
            type: GameParseType.GameInfo);

        Assert.Multiple(() =>
        {
            Assert.That(game.Developer.Value, Is.EqualTo("新开发商"));
            Assert.That(mutations, Has.Count.EqualTo(1));
            Assert.That(mutations[0].Game, Is.SameAs(game));
            Assert.That(mutations[0].Changes, Is.EqualTo(GalgameChangeKind.Metadata));
            Assert.That(mutations[0].Origin, Is.EqualTo(GalgameChangeOrigin.Parser));
            Assert.That(mutations[0].ParsedTypes, Is.EqualTo(GameParseType.GameInfo));
        });
        await WaitUntilAsync(() => DbSet.FindById(game.Uuid)?.Developer.Value == "新开发商",
            "解析结果未及时写入数据库");
    }

    #endregion

    #region 游玩状态同步

    // 验证下载游玩状态：搜刮器不支持状态同步时返回Other与提示信息
    [Test]
    public async Task DownLoadPlayStatusAsync_WithoutSyncSupport_ReturnsOther()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        // 默认mock只实现IGalInfoPhraser，不实现IGalStatusSync
        SetupPhraser(service, RssType.Ymgal, null);

        (GalStatusSyncResult result, string? msg) =
            await service.DownLoadPlayStatusAsync(CreateGame("游戏甲"), RssType.Ymgal);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(GalStatusSyncResult.Other));
            Assert.That(msg, Is.Not.Empty);
        });
    }

    // 验证下载游玩状态：搜刮器支持状态同步时委托给搜刮器并原样返回其结果
    [Test]
    public async Task DownLoadPlayStatusAsync_WithSyncSupport_DelegatesToPhraser()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        // Moq限制：Object被访问后不能再As<T>追加接口，所以先配好IGalStatusSync再取Object
        Mock<IGalInfoPhraser> phraser = new();
        phraser.Setup(x => x.GetPhraseType()).Returns(RssType.Bangumi);
        phraser.As<IGalStatusSync>().Setup(x => x.DownloadAsync(It.IsAny<Galgame>()))
            .ReturnsAsync((GalStatusSyncResult.Ok, "同步成功"));
        service.PhraserList[(int)RssType.Bangumi] = phraser.Object;
        Galgame game = CreateGame("游戏甲");

        (GalStatusSyncResult result, string? msg) = await service.DownLoadPlayStatusAsync(game, RssType.Bangumi);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(GalStatusSyncResult.Ok));
            Assert.That(msg, Is.EqualTo("同步成功"));
        });
    }

    // 验证上传游玩状态：搜刮器不支持状态同步时抛NotSupportedException
    [Test]
    public async Task UploadPlayStatusAsync_WithoutSyncSupport_ThrowsNotSupported()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        SetupPhraser(service, RssType.Ymgal, null);

        Assert.ThrowsAsync<NotSupportedException>(async () =>
            await service.UploadPlayStatusAsync(CreateGame("游戏甲"), RssType.Ymgal));
    }

    #endregion

    #region 数据导出

    // 验证导出：列表中的游戏以深克隆副本形式写入导出数据，进度回调逐游戏推进
    [Test]
    public async Task ExportAsync_ExportsDeepClonedGamesWithProgress()
    {
        GalgameCollectionService service = await CreateInitializedServiceAsync();
        Galgame gameA = CreateGame("导出游戏A");
        Galgame gameB = CreateGame("导出游戏B");
        await service.AddVirtualGalgameAsync(gameA);
        await service.AddVirtualGalgameAsync(gameB);
        List<(int current, int total)> progress = [];

        await service.ExportAsync((_, current, total) => progress.Add((current, total)));

        Assert.That(Settings.Exported.ContainsKey(KeyValues.Galgames), Is.True);
        Assert.That(Settings.Exported[KeyValues.Galgames], Is.TypeOf<ObservableCollection<Galgame>>());
        var exported = (ObservableCollection<Galgame>)Settings.Exported[KeyValues.Galgames];
        Assert.Multiple(() =>
        {
            Assert.That(exported.Select(g => g.Name.Value),
                Is.EquivalentTo(new[] { "导出游戏A", "导出游戏B" }));
            Assert.That(progress.Count, Is.EqualTo(2));
            Assert.That(progress[1], Is.EqualTo((2, 2)));
            // 导出的是深克隆副本，不是内存中的原对象
            Assert.That(exported.Single(g => g.Name.Value == "导出游戏A"), Is.Not.SameAs(gameA));
        });
    }

    #endregion
}
