using GalgameManager.Models;

namespace GalgameManager.Test.Models;

[TestFixture]
public class StorePluginTest
{
    [Test]
    public void UpdateStatus_WithoutInstalledVersion_MarksNotInstalled()
    {
        StorePlugin plugin = CreatePlugin("2.0", "1.0");
        plugin.UpdateStatus(new Version(1, 0));

        plugin.UpdateStatus(null);

        Assert.Multiple(() =>
        {
            Assert.That(plugin.Status, Is.EqualTo(StorePluginStatus.NotInstalled));
            Assert.That(plugin.InstalledVersion, Is.Null);
        });
    }

    [Test]
    public void UpdateStatus_WithLatestVersionInstalled_MarksInstalled()
    {
        StorePlugin plugin = CreatePlugin("2.0", "1.0");

        plugin.UpdateStatus(new Version(2, 0));

        Assert.Multiple(() =>
        {
            Assert.That(plugin.Status, Is.EqualTo(StorePluginStatus.Installed));
            Assert.That(plugin.InstalledVersion, Is.EqualTo(new Version(2, 0)));
        });
    }

    [Test]
    public void UpdateStatus_WithOlderVersionInstalled_MarksUpdateAvailable()
    {
        StorePlugin plugin = CreatePlugin("2.0", "1.0");

        plugin.UpdateStatus(new Version(1, 0));

        Assert.Multiple(() =>
        {
            Assert.That(plugin.Status, Is.EqualTo(StorePluginStatus.UpdateAvailable));
            Assert.That(plugin.InstalledVersion, Is.EqualTo(new Version(1, 0)));
        });
    }

    [Test]
    public void UpdateStatus_WithoutKnownVersions_MarksInstalled()
    {
        StorePlugin plugin = CreatePlugin();

        plugin.UpdateStatus(new Version(1, 0));

        Assert.That(plugin.Status, Is.EqualTo(StorePluginStatus.Installed));
    }

    /// <param name="versions">仓库里的版本号，按商店的约定从新到旧排列</param>
    private static StorePlugin CreatePlugin(params string[] versions)
    {
        return new StorePlugin
        {
            RepoName = "example",
            Versions = versions.Select(v => new StorePluginVersion { Version = new Version(v) }).ToList(),
        };
    }
}
