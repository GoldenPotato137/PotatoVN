using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class FolderOperationsTest
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(TestEnvironmentSetup.Root, TestContext.CurrentContext.Test.Name);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch
        {
            // ignore
        }
    }

    // 回归：曾只删除文件、最后用非递归Directory.Delete删除根目录，游戏文件夹含子文件夹时抛"目录不是空的"
    [Test]
    public void Delete_NestedFoldersAndReadOnlyFile_DeletesEverything()
    {
        // Arrange
        string game = Path.Combine(_root, "Game");
        Directory.CreateDirectory(Path.Combine(game, "save", "slot1"));
        File.WriteAllText(Path.Combine(game, "game.exe"), "exe");
        File.WriteAllText(Path.Combine(game, "save", "slot1", "1.sav"), "sav");
        string readOnly = Path.Combine(game, "save", "readonly.dat");
        File.WriteAllText(readOnly, "ro");
        new FileInfo(readOnly).IsReadOnly = true;
        List<string> deleted = [];

        // Act
        FolderOperations.Delete(game, info => deleted.Add(info.Name));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(game), Is.False);
            Assert.That(deleted, Is.EquivalentTo(new[] { "game.exe", "1.sav", "readonly.dat" }));
        });
    }

    [Test]
    public void Delete_FolderSymbolicLink_KeepsLinkTarget()
    {
        // Arrange
        string target = Path.Combine(_root, "RealSave");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "1.sav"), "sav");
        string game = Path.Combine(_root, "Game");
        Directory.CreateDirectory(game);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(game, "save"), target);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            Assert.Ignore($"Creating symbolic links requires Developer Mode or admin: {e.Message}");
        }

        // Act
        FolderOperations.Delete(game);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(game), Is.False);
            Assert.That(File.Exists(Path.Combine(target, "1.sav")), Is.True);
        });
    }

    // 链接目标已被删除（如云盘文件夹被移走）时，失效链接也应能正常删除
    [Test]
    public void Delete_BrokenFolderSymbolicLink_DeletesLink()
    {
        // Arrange
        string target = Path.Combine(_root, "RealSave");
        Directory.CreateDirectory(target);
        string game = Path.Combine(_root, "Game");
        Directory.CreateDirectory(game);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(game, "save"), target);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            Assert.Ignore($"Creating symbolic links requires Developer Mode or admin: {e.Message}");
        }
        Directory.Delete(target);

        // Act
        FolderOperations.Delete(game);

        // Assert
        Assert.That(Directory.Exists(game), Is.False);
    }
}
