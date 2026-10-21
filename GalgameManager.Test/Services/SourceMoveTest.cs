using System.IO.Compression;
using GalgameManager.Core.Services;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Services;

namespace GalgameManager.Test.Services;

/// <summary>
/// 移动游戏到其他库（本地库与压缩库之间）相关的来源决策、路径决策与解压任务测试
/// </summary>
[TestFixture]
public class SourceMoveTest : ServiceTestBase
{
    [Test]
    public void ResolveSourceEntry_PrefersMoveOutEntryWithFiles()
    {
        // Arrange
        Galgame game = new("Example");
        GalgameFolderSource folder = new(CreateDir("folderLib"));
        GalgameZipSource zip = new(CreateDir("zipLib"));
        folder.AddGalgame(game, Path.Combine(folder.Path, "Example"));
        GalgameAndPath zipEntry = zip.AddGalgame(game, Path.Combine(zip.Path, "Example.zip"));

        // Act
        GalgameAndPath? result = SourceMoveTask.ResolveSourceEntry(game, zip);

        // Assert
        Assert.That(result, Is.SameAs(zipEntry));
    }

    [Test]
    public void ResolveSourceEntry_NoMoveOut_UsesPreferredLocalInstallation()
    {
        // Arrange
        Galgame game = new("Example");
        GalgameZipSource zip = new(CreateDir("zipLib"));
        GalgameFolderSource folder = new(CreateDir("folderLib"));
        zip.AddGalgame(game, Path.Combine(zip.Path, "Example.zip"));
        GalgameAndPath folderEntry = folder.AddGalgame(game, Path.Combine(folder.Path, "Example"));

        // Act
        GalgameAndPath? result = SourceMoveTask.ResolveSourceEntry(game, null);

        // Assert
        Assert.That(result, Is.SameAs(folderEntry));
    }

    [Test]
    public void ResolveSourceEntry_ArchiveOnlyGame_UsesZipEntry()
    {
        // Arrange
        Galgame game = new("Example");
        GalgameZipSource zip = new(CreateDir("zipLib"));
        GalgameAndPath zipEntry = zip.AddGalgame(game, Path.Combine(zip.Path, "Example.zip"));

        // Act
        GalgameAndPath? result = SourceMoveTask.ResolveSourceEntry(game, new VirtualSource());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(zipEntry));
            Assert.That(game.SourceEntries.Any(SourceMoveTask.HasFiles), Is.True);
            Assert.That(game.IsLocalGame, Is.False);
        });
    }

    [Test]
    public void ResolveSourceEntry_NoFiles_ReturnsNull()
    {
        Galgame game = new("Example");
        Assert.That(SourceMoveTask.ResolveSourceEntry(game, null), Is.Null);
    }

    [Test]
    public void GetMoveInPath_UsesPackNameForArchiveAndFolderNameForFolder()
    {
        // Arrange
        Galgame game = new("Example");
        GalgameFolderSource target = new(CreateDir("targetLib"));
        GalgameZipSource zip = new(CreateDir("zipLib"));
        GalgameFolderSource folder = new(CreateDir("folderLib"));
        GalgameAndPath zipEntry = zip.AddGalgame(game, Path.Combine(zip.Path, "Fate.Stay.Night.part1.rar"));
        GalgameAndPath folderEntry = folder.AddGalgame(game, Path.Combine(folder.Path, "MyGame"));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(LocalFolderSourceService.GetMoveInPath(target, zipEntry),
                Is.EqualTo(Path.Combine(target.Path, "Fate.Stay.Night")));
            Assert.That(LocalFolderSourceService.GetMoveInPath(target, folderEntry),
                Is.EqualTo(Path.Combine(target.Path, "MyGame")));
        });
    }

    // 压缩包根目录同时存在零散文件和一个文件夹时，不能只取那个文件夹（回归：曾丢失根目录文件）
    [Test]
    public async Task UnpackGameTask_RootFilesAndOneFolder_KeepsEverything()
    {
        // Arrange
        string content = CreateDir("content");
        await File.WriteAllTextAsync(Path.Combine(content, "game.exe"), "exe");
        Directory.CreateDirectory(Path.Combine(content, "data"));
        await File.WriteAllTextAsync(Path.Combine(content, "data", "a.dat"), "dat");
        string packPath = Path.Combine(CreateDir("zipLib"), "MyGame.zip");
        ZipFile.CreateFromDirectory(content, packPath);
        string targetRoot = CreateDir("targetLib");
        UnpackGameTask task = new(packPath, targetRoot, "MyGame") { AddToLibrary = false };

        // Act
        await CreateBgTaskService().AddBgTask(task);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(task.Task.IsFaulted, Is.False);
            Assert.That(task.ResultPath, Is.EqualTo(Path.Combine(targetRoot, "MyGame")));
            Assert.That(File.Exists(Path.Combine(task.ResultPath, "game.exe")), Is.True);
            Assert.That(File.Exists(Path.Combine(task.ResultPath, "data", "a.dat")), Is.True);
            Assert.That(Directory.GetDirectories(targetRoot), Has.Length.EqualTo(1)); // 临时目录已清理
        });
    }

    // 回归：解压后压缩包句柄未释放，导致移动时紧接着的"删除压缩包"失败（文件被占用）
    [Test]
    public async Task UnpackGameTask_ReleasesArchiveAfterUnpack()
    {
        // Arrange
        string content = CreateDir("content");
        await File.WriteAllTextAsync(Path.Combine(content, "game.exe"), "exe");
        string packPath = Path.Combine(CreateDir("zipLib"), "MyGame.zip");
        ZipFile.CreateFromDirectory(content, packPath);
        UnpackGameTask task = new(packPath, CreateDir("targetLib"), "MyGame") { AddToLibrary = false };

        // Act
        await CreateBgTaskService().AddBgTask(task);

        // Assert
        Assert.That(task.Task.IsFaulted, Is.False);
        Assert.DoesNotThrow(() => File.Delete(packPath));
    }

    [Test]
    public async Task UnpackGameTask_SingleTopFolder_IsUsedAsGameFolder()
    {
        // Arrange
        string content = CreateDir("content");
        Directory.CreateDirectory(Path.Combine(content, "Inner"));
        await File.WriteAllTextAsync(Path.Combine(content, "Inner", "game.exe"), "exe");
        string packPath = Path.Combine(CreateDir("zipLib"), "MyGame.zip");
        ZipFile.CreateFromDirectory(content, packPath);
        string targetRoot = CreateDir("targetLib");
        UnpackGameTask task = new(packPath, targetRoot, "MyGame") { AddToLibrary = false };

        // Act
        await CreateBgTaskService().AddBgTask(task);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(task.Task.IsFaulted, Is.False);
            Assert.That(File.Exists(Path.Combine(task.ResultPath, "game.exe")), Is.True);
        });
    }

    // 移动游戏时解压失败必须让任务fault，否则SourceMoveTask会把不存在的路径登记到游戏上
    [Test]
    public async Task UnpackGameTask_BrokenArchive_NotAddToLibrary_TaskFaultsAndCleansUp()
    {
        // Arrange
        string packPath = Path.Combine(CreateDir("zipLib"), "MyGame.zip");
        await File.WriteAllTextAsync(packPath, "this is not a zip");
        string targetRoot = CreateDir("targetLib");
        UnpackGameTask task = new(packPath, targetRoot, "MyGame") { AddToLibrary = false };

        // Act
        await CreateBgTaskService().AddBgTask(task);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(task.Task.IsFaulted, Is.True);
            Assert.That(Directory.GetFileSystemEntries(targetRoot), Is.Empty);
        });
    }

    [Test]
    public async Task UnpackGameTask_PackMissing_NotAddToLibrary_TaskFaults()
    {
        UnpackGameTask task = new(Path.Combine(TestDir, "missing.zip"), CreateDir("targetLib"), "MyGame")
            { AddToLibrary = false };

        await CreateBgTaskService().AddBgTask(task);

        Assert.That(task.Task.IsFaulted, Is.True);
    }

    // 压缩后再解压：PackGameTask的产物能被UnpackGameTask还原为同样的游戏文件夹
    [Test]
    public async Task PackThenUnpack_RoundTripsGameFolder()
    {
        // Arrange
        Galgame game = new("Example");
        GalgameFolderSource folder = new(CreateDir("folderLib"));
        string gameFolder = CreateDir("folderLib/MyGame");
        await File.WriteAllTextAsync(Path.Combine(gameFolder, "game.exe"), "exe");
        Directory.CreateDirectory(Path.Combine(gameFolder, "save"));
        await File.WriteAllTextAsync(Path.Combine(gameFolder, "save", "1.sav"), "sav");
        GalgameAndPath entry = folder.AddGalgame(game, gameFolder);
        GalgameZipSource zip = new(CreateDir("zipLib"));
        PackGameTask pack = new(game, entry, zip, ZipSourceService.GetPackPath(zip, entry));
        BgTaskService bgTaskService = CreateBgTaskService();
        await bgTaskService.AddBgTask(pack);
        string targetRoot = CreateDir("targetLib");
        UnpackGameTask unpack = new(pack.ResultPath, targetRoot, "MyGame") { AddToLibrary = false };

        // Act
        await bgTaskService.AddBgTask(unpack);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(pack.Task.IsFaulted, Is.False);
            Assert.That(unpack.Task.IsFaulted, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(unpack.ResultPath, "game.exe")), Is.EqualTo("exe"));
            Assert.That(File.ReadAllText(Path.Combine(unpack.ResultPath, "save", "1.sav")), Is.EqualTo("sav"));
        });
    }

    private BgTaskService CreateBgTaskService() => new(InfoService.Object, new FileService(), CreateServiceProvider());
}
