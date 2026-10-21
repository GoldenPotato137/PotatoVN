using GalgameManager.Models;
using Newtonsoft.Json;

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

    [Test]
    public void Downloads_SumsAllVersions()
    {
        StorePlugin plugin = CreatePlugin("2.0", "1.1", "1.0");
        plugin.Versions[0].Downloads = 100;
        plugin.Versions[1].Downloads = 20;
        plugin.Versions[2].Downloads = 3;

        Assert.That(plugin.Downloads, Is.EqualTo(123));
    }

    [Test]
    public void Downloads_WithoutVersions_IsZero()
    {
        Assert.That(CreatePlugin().Downloads, Is.Zero);
    }

    [Test]
    public void ToInstallStorePlugin_WithManyVersions_FitsInMsixLocalSettings()
    {
        StorePlugin plugin = CreatePlugin(Enumerable.Range(0, 100).Select(i => $"1.{i}").ToArray());
        plugin.Id = Guid.NewGuid();
        plugin.Name = "游戏库增强";
        plugin.DescriptionDetailed = new string('长', 5000);
        foreach (StorePluginVersion v in plugin.Versions)
            v.DownloadUrl = $"https://plugin.api.potatovn.net/api/universal/pvn-plugin/{plugin.RepoName}/plugin/{v.Version}/plugin.pvnplugin.zip";
        StorePluginVersion version = plugin.Versions[0];

        var json = JsonConvert.SerializeObject(new List<ToInstallStorePlugin> { ToInstallStorePlugin.Create(plugin, version) },
            new JsonSerializerSettings());
        ToInstallStorePlugin result = JsonConvert.DeserializeObject<List<ToInstallStorePlugin>>(json)!.Single();

        Assert.Multiple(() =>
        {
            // LocalSettings单个设置上限8KB（UTF-16），留足余量给同时排队多个插件
            Assert.That(json.Length * 2, Is.LessThan(1024));
            Assert.That(result.Id, Is.EqualTo(plugin.Id));
            Assert.That(result.Name, Is.EqualTo(plugin.Name));
            Assert.That(result.Version, Is.EqualTo(version.Version));
            Assert.That(result.DownloadUrl, Is.EqualTo(version.DownloadUrl));
        });
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
