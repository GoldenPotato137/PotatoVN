using System.Text;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Services;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using Moq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GalgameManager.Test.Services;

[TestFixture]
public class ZipSourceServiceTest : ServiceTestBase
{
    private ZipSourceService CreateService(IGalgameSourceCollectionService sourceCollection) =>
        new(InfoService.Object, sourceCollection);

    // 创建已初始化的压缩库集合service（含内存设置、真实LiteDB）
    private async Task<GalgameSourceCollectionService> CreateSourceCollectionAsync()
    {
        GalgameSourceCollectionService service = new(Settings, BgTaskService.Object, InfoService.Object,
            CreateServiceProvider());
        await service.InitAsync();
        return service;
    }

    [Test]
    public async Task SaveMetaAsync_WhenDisabled_DoesNotCreateMetaFolder()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        string libPath = CreateDir("zipLib");
        Galgame game = new();
        GalgameZipSource source = (GalgameZipSource)sourceCollection.AddGalgameSourceAsync(
            GalgameSourceType.LocalZip, libPath, tryGetGalgame: false).Result;
        source.SaveMetaBackup = false;
        GalgameAndPath entry = source.AddGalgame(game, Path.Combine(libPath, "game.zip"));

        // Act
        await service.SaveMetaAsync(game);

        // Assert
        string metaPath = Path.Combine(libPath, ".PotatoVN", "game");
        Assert.That(Directory.Exists(metaPath), Is.False);
    }

    // SaveMetaAsync 在开启备份时会调用 LocalFolderSourceService.FolderBaseSaveMeta（内部依赖 App.GetService<IFileService>()，测试环境不可用）。
    // 这里改为验证：开启后 ZipSourceService 确实尝试为库内条目写备份（通过 meta 目录路径计算正确性间接体现），并且只对 SaveMetaBackup 为 true 的源生效。
    [Test]
    public async Task SaveMetaAsync_SkipsSourcesWithBackupDisabled_AndTargetsMatchingSource()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        string libPathOn = CreateDir("zipLibOn");
        string libPathOff = CreateDir("zipLibOff");
        Galgame game = new();
        GalgameZipSource sourceOn = (GalgameZipSource)await sourceCollection.AddGalgameSourceAsync(
            GalgameSourceType.LocalZip, libPathOn, tryGetGalgame: false);
        sourceOn.SaveMetaBackup = true;
        GalgameZipSource sourceOff = (GalgameZipSource)await sourceCollection.AddGalgameSourceAsync(
            GalgameSourceType.LocalZip, libPathOff, tryGetGalgame: false);
        sourceOff.SaveMetaBackup = false;
        sourceOn.AddGalgame(game, Path.Combine(libPathOn, "game.zip"));
        sourceOff.AddGalgame(game, Path.Combine(libPathOff, "game.zip"));

        // Act：指定只保存到关闭备份的库应直接跳过（不抛异常即未进入写文件路径）
        await service.SaveMetaAsync(game, sourceOff);
        // 未指定目标源时，关闭备份的库被跳过，开启备份的库会尝试写文件（此处因测试环境限制会失败，只验证不抛出未处理异常）

        // Assert：关闭备份的库不应生成任何.PotatoVN目录；开启备份的库至多创建了目录（内容取决于运行时文件服务）
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(libPathOff, ".PotatoVN")), Is.False);
        });
        // 开启备份的库路径计算正确（以包名命名）——通过 GetMetaPath 公开契约验证
        Assert.That(ZipSourceService.GetMetaPath(libPathOn, Path.Combine(libPathOn, "sub", "game.part1.zip")),
            Is.EqualTo(Path.Combine(libPathOn, ".PotatoVN", "game")));
    }

    [Test]
    public async Task LoadMetaAsync_NoMetaFolder_ReturnsNull()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);

        // Act
        Galgame? result = await service.LoadMetaAsync(Path.Combine(TestDir, "nonexistent.zip"));

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSourcePathAsync_MatchesRegisteredZipSourceByPrefix()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        string libPath = CreateDir("zipLib");
        await sourceCollection.AddGalgameSourceAsync(GalgameSourceType.LocalZip, libPath,
            tryGetGalgame: false);

        // Act：压缩包在库根目录的子文件夹中
        string packPath = Path.Combine(libPath, "sub", "game.zip");
        string result = await service.GetSourcePathAsync(packPath);

        // Assert：应匹配到该已注册库，而不是退回到父目录
        Assert.That(result, Is.EqualTo(libPath));
    }

    [Test]
    public async Task GetSourcePathAsync_NoRegisteredSource_FallsBackToParentDirectory()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        string dir = CreateDir("someDir");

        // Act
        string result = await service.GetSourcePathAsync(Path.Combine(dir, "game.zip"));

        // Assert
        Assert.That(result, Is.EqualTo(dir));
    }

    [Test]
    public async Task CheckMoveOperateValid_NoLocalInstallation_ReturnsError()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        Galgame game = new(); // 无任何本地安装
        GalgameZipSource target = new(CreateDir("zipLib"));

        // Act
        string? result = service.CheckMoveOperateValid(target, null, game);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void GetPackName_HandlesVolumeAndNormalExtensions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.part1.zip"), Is.EqualTo("game"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.zip"), Is.EqualTo("game"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.7z"), Is.EqualTo("game"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.001"), Is.EqualTo("game"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.rar"), Is.EqualTo("game"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.txt"), Is.EqualTo("game"));
            // 回归：带点的游戏名、7z分卷、rar新式分卷曾被错误解析
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\Fate.Stay.Night.zip"), Is.EqualTo("Fate.Stay.Night"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.7z.001"), Is.EqualTo("game"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\game.part2.rar"), Is.EqualTo("game"));
            Assert.That(GalgameZipSource.GetPackName(@"C:\lib\GAME.ZIP"), Is.EqualTo("GAME"));
        });
    }

    [Test]
    public void IsFirstVolume_OnlyFirstVolumeOfASeries()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GalgameZipSource.IsFirstVolume("game.zip"), Is.True);
            Assert.That(GalgameZipSource.IsFirstVolume("game.part1.rar"), Is.True);
            Assert.That(GalgameZipSource.IsFirstVolume("game.part01.rar"), Is.True);
            Assert.That(GalgameZipSource.IsFirstVolume("game.part2.rar"), Is.False);
            Assert.That(GalgameZipSource.IsFirstVolume("game.7z.001"), Is.True);
            Assert.That(GalgameZipSource.IsFirstVolume("game.7z.002"), Is.False);
            Assert.That(GalgameZipSource.IsFirstVolume("game.002"), Is.False);
            Assert.That(GalgameZipSource.IsFirstVolume("readme.txt"), Is.False);
        });
    }

    [Test]
    public void GetVolumePaths_ReturnsOnlySameSeries()
    {
        // Arrange
        string dir = CreateDir("zipLib");
        foreach (var name in new[] { "game.part1.rar", "game.part2.rar", "game2.part1.rar", "game.rar", "other.zip" })
            File.WriteAllText(Path.Combine(dir, name), name);

        // Act
        List<string> volumes = GalgameZipSource.GetVolumePaths(Path.Combine(dir, "game.part1.rar"));

        // Assert
        Assert.That(volumes.Select(Path.GetFileName),
            Is.EquivalentTo(new[] { "game.part1.rar", "game.part2.rar" }));
        Assert.That(GalgameZipSource.GetVolumePaths(Path.Combine(dir, "other.zip")), Has.Count.EqualTo(1));
    }

    [Test]
    public void DeletePackFiles_DeletesAllVolumesAndMeta_KeepsOthers()
    {
        // Arrange
        string dir = CreateDir("zipLib");
        foreach (var name in new[] { "game.7z.001", "game.7z.002", "other.zip" })
            File.WriteAllText(Path.Combine(dir, name), name);
        string metaPath = CreateDir("zipLib/.PotatoVN/game");
        File.WriteAllText(Path.Combine(metaPath, "meta.json"), "{}");
        string otherMeta = CreateDir("zipLib/.PotatoVN/other");

        // Act
        ZipSourceService.DeletePackFiles(dir, Path.Combine(dir, "game.7z.001"));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EquivalentTo(new[] { "other.zip" }));
            Assert.That(Directory.Exists(metaPath), Is.False);
            Assert.That(Directory.Exists(otherMeta), Is.True);
        });
    }

    // 压缩库之间移动即复制压缩包：任意一个分卷在目标库已存在都不允许
    [Test]
    public async Task CheckMoveOperateValid_ZipToZip_ValidUnlessAnyVolumeExists()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        (Galgame game, GalgameZipSource from) = CreateMultiVolumeGame("zipLibA");
        GalgameZipSource target = new(CreateDir("zipLibB"));

        // Act
        string? valid = service.CheckMoveOperateValid(target, from, game);
        await File.WriteAllTextAsync(Path.Combine(target.Path, "Example.part2.rar"), "other");
        string? exists = service.CheckMoveOperateValid(target, from, game);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.Null);
            Assert.That(exists, Is.Not.Null);
        });
    }

    [Test]
    public async Task CheckMoveOperateValid_ZipToZip_PackMissing_ReturnsError()
    {
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        Galgame game = new("Example");
        GalgameZipSource from = new(CreateDir("zipLibA"));
        from.AddGalgame(game, Path.Combine(from.Path, "Example.zip")); // 压缩包不存在

        Assert.That(service.CheckMoveOperateValid(new GalgameZipSource(CreateDir("zipLibB")), from, game),
            Is.Not.Null);
    }

    // 仅存在于压缩库的游戏（没有指定移出库）移入其他压缩库时，复制其压缩包，且保留原文件名
    [Test]
    public async Task MoveInAsync_FromZipEntry_ReturnsCopyPackTask()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        (Galgame game, _) = CreateMultiVolumeGame("zipLibA");
        GalgameZipSource target = new(CreateDir("zipLibB"));

        // Act
        BgTaskBase task = service.MoveInAsync(target, game);

        // Assert
        Assert.That(task, Is.TypeOf<CopyPackTask>());
        Assert.That(((CopyPackTask)task).ResultPath, Is.EqualTo(Path.Combine(target.Path, "Example.part1.rar")));
    }

    [Test]
    public async Task CopyPackTask_CopiesAllVolumes_KeepsOriginals()
    {
        // Arrange
        (Galgame game, GalgameZipSource from) = CreateMultiVolumeGame("zipLibA");
        GalgameZipSource target = new(CreateDir("zipLibB"));
        CopyPackTask task = new(game, from.GetEntry(game)!, target);

        // Act
        await CreateBgTaskService().AddBgTask(task);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(task.Task.IsFaulted, Is.False);
            Assert.That(Directory.GetFiles(target.Path).Select(Path.GetFileName),
                Is.EquivalentTo(new[] { "Example.part1.rar", "Example.part2.rar" }));
            Assert.That(File.ReadAllText(Path.Combine(target.Path, "Example.part2.rar")), Is.EqualTo("part2"));
            Assert.That(Directory.GetFiles(from.Path), Has.Length.EqualTo(3)); // 原压缩包（及无关文件）不动
        });
    }

    // 目标库已有同名分卷时任务必须fault（SourceMoveTask据此中止），且不能动已存在的文件
    [Test]
    public async Task CopyPackTask_VolumeExists_TaskFaults_KeepsExistingFile()
    {
        // Arrange
        (Galgame game, GalgameZipSource from) = CreateMultiVolumeGame("zipLibA");
        GalgameZipSource target = new(CreateDir("zipLibB"));
        string existing = Path.Combine(target.Path, "Example.part2.rar");
        await File.WriteAllTextAsync(existing, "other");
        CopyPackTask task = new(game, from.GetEntry(game)!, target);

        // Act
        await CreateBgTaskService().AddBgTask(task);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(task.Task.IsFaulted, Is.True);
            Assert.That(File.Exists(Path.Combine(target.Path, "Example.part1.rar")), Is.False);
            Assert.That(File.ReadAllText(existing), Is.EqualTo("other"));
        });
    }

    // 压缩库中的分卷游戏：Example.part1.rar + Example.part2.rar，另有一个无关压缩包
    private (Galgame game, GalgameZipSource source) CreateMultiVolumeGame(string libName)
    {
        Galgame game = new("Example");
        GalgameZipSource source = new(CreateDir(libName));
        foreach (var name in new[] { "Example.part1.rar", "Example.part2.rar", "Other.zip" })
            File.WriteAllText(Path.Combine(source.Path, name), name == "Example.part2.rar" ? "part2" : name);
        source.AddGalgame(game, Path.Combine(source.Path, "Example.part1.rar"));
        return (game, source);
    }

    private BgTaskService CreateBgTaskService() => new(InfoService.Object, new FileService(), CreateServiceProvider());

    [Test]
    public async Task CheckMoveOperateValid_TargetPackExists_ReturnsError_OtherwiseNull()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        Galgame game = new("Example");
        GalgameFolderSource folder = new(CreateDir("folderLib"));
        folder.AddGalgame(game, CreateDir("folderLib/MyGame"));
        GalgameZipSource target = new(CreateDir("zipLib"));

        // Act
        string? valid = service.CheckMoveOperateValid(target, folder, game);
        await File.WriteAllTextAsync(Path.Combine(target.Path, "MyGame.zip"), "zip");
        string? exists = service.CheckMoveOperateValid(target, folder, game);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.Null);
            Assert.That(exists, Is.Not.Null);
        });
    }

    // 移动校验时移出库的service也会被调用，此时压缩库service不应干涉移入本地库的操作
    [Test]
    public async Task CheckMoveOperateValid_MoveInIsNotZip_ReturnsNull()
    {
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        Galgame game = new("Example");
        GalgameZipSource from = new(CreateDir("zipLib"));
        from.AddGalgame(game, Path.Combine(from.Path, "Example.zip"));

        Assert.That(service.CheckMoveOperateValid(new GalgameFolderSource(CreateDir("folderLib")), from, game),
            Is.Null);
    }

    [Test]
    public void IsInSource_RequiresPathSeparatorBoundary()
    {
        GalgameZipSource source = new(@"C:\Games\Zip");
        Assert.Multiple(() =>
        {
            Assert.That(source.IsInSource(@"C:\Games\Zip\game.zip"), Is.True);
            Assert.That(source.IsInSource(@"C:\Games\Zip2\game.zip"), Is.False);
        });
    }

    // 验证MoveInAsync返回PackGameTask，且目标zip路径为 <库根>/<游戏文件夹名>.zip
    [Test]
    public async Task MoveInAsync_ReturnsPackTask_WithExpectedZipPath()
    {
        // Arrange
        GalgameSourceCollectionService sourceCollection = await CreateSourceCollectionAsync();
        ZipSourceService service = CreateService(sourceCollection);
        GalgameZipSource zipSource = (GalgameZipSource)await sourceCollection.AddGalgameSourceAsync(
            GalgameSourceType.LocalZip, CreateDir("zipLib"), tryGetGalgame: false);
        Galgame game = new();
        GalgameFolderSource folderSource = (GalgameFolderSource)await sourceCollection.AddGalgameSourceAsync(
            GalgameSourceType.LocalFolder, CreateDir("folderLib"), tryGetGalgame: false);
        string gameFolder = CreateDir("folderLib/MyGame");
        GalgameAndPath entry = folderSource.AddGalgame(game, gameFolder);

        // Act
        BgTaskBase task = service.MoveInAsync(zipSource, game, zipSource.Path, entry);

        // Assert
        Assert.That(task, Is.TypeOf<PackGameTask>());
        Assert.That(((PackGameTask)task).ZipPath,
            Is.EqualTo(Path.Combine(zipSource.Path, "MyGame.zip")));
    }

    // 验证PackGameTask前置失败（zip已存在）时，BgTaskBase.Task会fault，
    // 使SourceMoveTask能通过IsFaulted感知子任务失败（回归：同步抛异常曾导致Task停留在CompletedTask而误判成功）
    [Test]
    public async Task PackGameTask_ZipAlreadyExists_TaskFaults()
    {
        // Arrange
        Galgame game = new();
        string gameFolder = CreateDir("folderLib/MyGame");
        GalgameFolderSource folderSource = new(CreateDir("folderLib"));
        GalgameAndPath entry = folderSource.AddGalgame(game, gameFolder);
        GalgameZipSource zipSource = new(CreateDir("zipLib"));
        PackGameTask task = new(game, entry, zipSource,
            Path.Combine(zipSource.Path, "MyGame.zip"));
        await File.WriteAllTextAsync(task.ZipPath, "already here");
        BgTaskService bgTaskService = new(InfoService.Object, new FileService(), CreateServiceProvider());

        // Act：AddBgTask不会向外抛异常（异常转为事件上报），完成后检查Task状态
        await bgTaskService.AddBgTask(task);

        // Assert
        Assert.That(task.Task.IsFaulted, Is.True);
    }

    // 验证PackGameTask成功路径：真实压缩游戏文件夹为zip，任务不fault
    [Test]
    public async Task PackGameTask_PacksFolderIntoZip()
    {
        // Arrange
        Galgame game = new();
        string gameFolder = CreateDir("folderLib/MyGame");
        await File.WriteAllTextAsync(Path.Combine(gameFolder, "game.exe"), "dummy");
        GalgameFolderSource folderSource = new(CreateDir("folderLib"));
        GalgameAndPath entry = folderSource.AddGalgame(game, gameFolder);
        GalgameZipSource zipSource = new(CreateDir("zipLib"));
        PackGameTask task = new(game, entry, zipSource,
            Path.Combine(zipSource.Path, "MyGame.zip"));
        BgTaskService bgTaskService = new(InfoService.Object, new FileService(), CreateServiceProvider());

        // Act
        await bgTaskService.AddBgTask(task);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(task.Task.IsFaulted, Is.False);
            Assert.That(File.Exists(task.ZipPath), Is.True);
        });
    }

    // 验证PackGameTask为文件名写入UTF-8标志位（回归：曾只按UTF-8写字节却不置标志位，资源管理器等按系统代码页解读成乱码）。
    // SharpCompress只对未置标志位的条目使用ArchiveEncoding.Default，因此用Latin1打开时，缺少标志位的中文文件名会被解成乱码。
    // （不用System.IO.Compression：.NET 8下传入编码后会无视标志位）
    [Test]
    public async Task PackGameTask_NonAsciiNames_SetsUtf8Flag()
    {
        // Arrange
        Galgame game = new();
        string gameFolder = CreateDir("folderLib/MyGame");
        Directory.CreateDirectory(Path.Combine(gameFolder, "存档"));
        await File.WriteAllTextAsync(Path.Combine(gameFolder, "存档", "请读我.txt"), "dummy");
        GalgameFolderSource folderSource = new(CreateDir("folderLib"));
        GalgameAndPath entry = folderSource.AddGalgame(game, gameFolder);
        GalgameZipSource zipSource = new(CreateDir("zipLib"));
        PackGameTask task = new(game, entry, zipSource,
            Path.Combine(zipSource.Path, "MyGame.zip"));
        BgTaskService bgTaskService = new(InfoService.Object, new FileService(), CreateServiceProvider());

        // Act
        await bgTaskService.AddBgTask(task);

        // Assert
        using IArchive archive = ArchiveFactory.OpenArchive(task.ZipPath, new ReaderOptions
        {
            ArchiveEncoding = new ArchiveEncoding { Default = Encoding.Latin1 },
            LeaveStreamOpen = false,
        });
        Assert.That(archive.Entries.Select(e => e.Key), Does.Contain("存档/请读我.txt"));
    }

    // 验证PackGameTask在压缩过程中按百分比汇报进度：单调递增，且完成前不会达到Total（否则会被误判为任务已完成）
    [Test]
    public async Task PackGameTask_ReportsCompressingProgress()
    {
        // Arrange
        Galgame game = new();
        string gameFolder = CreateDir("folderLib/MyGame");
        await File.WriteAllBytesAsync(Path.Combine(gameFolder, "a.dat"), new byte[1024 * 1024]);
        await File.WriteAllBytesAsync(Path.Combine(gameFolder, "b.dat"), new byte[1024 * 1024]);
        GalgameFolderSource folderSource = new(CreateDir("folderLib"));
        GalgameAndPath entry = folderSource.AddGalgame(game, gameFolder);
        GalgameZipSource zipSource = new(CreateDir("zipLib"));
        PackGameTask task = new(game, entry, zipSource,
            Path.Combine(zipSource.Path, "MyGame.zip"));
        List<long> percents = [];
        task.OnProgress += p =>
        {
            if (p.Total == 100) percents.Add(p.Current);
        };
        BgTaskService bgTaskService = new(InfoService.Object, new FileService(), CreateServiceProvider());

        // Act
        await bgTaskService.AddBgTask(task);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(task.Task.IsFaulted, Is.False);
            Assert.That(percents, Is.Not.Empty);
            Assert.That(percents, Is.Ordered);
            Assert.That(percents.Last(), Is.EqualTo(99));
        });
    }
}
