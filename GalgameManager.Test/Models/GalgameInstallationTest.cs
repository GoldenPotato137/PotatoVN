using GalgameManager.Models;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.WinApp.Base.Contracts;
using Newtonsoft.Json;

namespace GalgameManager.Test.Models;

[TestFixture]
public class GalgameInstallationTest
{
    [Test]
    public void DifferentSources_CanContainSameGame_WithIndependentConfiguration()
    {
        Galgame game = new("Example");
        GalgameFolderSource sourceA = CreateSource(@"C:\LibraryA", "Library A");
        GalgameFolderSource sourceB = CreateSource(@"D:\LibraryB", "Library B");

        GalgameAndPath first = sourceA.AddGalgame(game, @"C:\LibraryA\Example",
            localConfig: new LocalInstallationConfig { ExePath = @"C:\LibraryA\Example\a.exe" });
        GalgameAndPath second = sourceB.AddGalgame(game, @"D:\LibraryB\Example",
            localConfig: new LocalInstallationConfig { ExePath = @"D:\LibraryB\Example\b.exe" });

        Assert.Multiple(() =>
        {
            Assert.That(game.LocalInstallations, Has.Count.EqualTo(2));
            Assert.That(game.Sources, Has.Count.EqualTo(2));
            Assert.That(first.LocalConfig!.ExePath, Is.Not.EqualTo(second.LocalConfig!.ExePath));
            Assert.That(game.PreferredInstallationId, Is.EqualTo(first.EntryId));
            Assert.That(game.LocalPath, Is.EqualTo(first.Path));
        });
    }

    [Test]
    public void RemovingPreferredInstallation_SelectsRemainingInstallation()
    {
        Galgame game = new("Example");
        GalgameFolderSource sourceA = CreateSource(@"C:\LibraryA", "Library A");
        GalgameFolderSource sourceB = CreateSource(@"D:\LibraryB", "Library B");
        GalgameAndPath first = sourceA.AddGalgame(game, @"C:\LibraryA\Example");
        GalgameAndPath second = sourceB.AddGalgame(game, @"D:\LibraryB\Example");
        game.SetPreferredInstallation(first);

        sourceA.DeleteGalgame(game);

        Assert.Multiple(() =>
        {
            Assert.That(game.LocalInstallations, Has.Count.EqualTo(1));
            Assert.That(game.PreferredInstallationId, Is.EqualTo(second.EntryId));
            Assert.That(game.IsLocalGame, Is.True);
        });
    }

    [Test]
    public void LoadingEntries_DoesNotOverwritePersistedPreferredInstallation()
    {
        Galgame game = new("Example");
        Guid firstId = Guid.NewGuid();
        Guid preferredId = Guid.NewGuid();
        game.PreferredInstallationId = preferredId;
        GalgameFolderSource sourceA = CreateSource(@"C:\LibraryA", "Library A");
        GalgameFolderSource sourceB = CreateSource(@"D:\LibraryB", "Library B");

        sourceA.AddGalgame(game, @"C:\LibraryA\Example", firstId);
        GalgameAndPath preferred = sourceB.AddGalgame(game, @"D:\LibraryB\Example", preferredId);
        game.EnsurePreferredInstallation();

        Assert.Multiple(() =>
        {
            Assert.That(game.PreferredInstallationId, Is.EqualTo(preferredId));
            Assert.That(game.PreferredLocalInstallation, Is.SameAs(preferred));
        });
    }

    [Test]
    public void RemovingLastInstallation_KeepsLogicalGameWithoutLocalState()
    {
        Galgame game = new("Example");
        GalgameFolderSource source = CreateSource(@"C:\Library", "Library");
        source.AddGalgame(game, @"C:\Library\Example");

        source.DeleteGalgame(game);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsLocalGame, Is.False);
            Assert.That(game.LocalInstallations, Is.Empty);
            Assert.That(game.PreferredInstallationId, Is.Null);
            Assert.That(game.Name.Value, Is.EqualTo("Example"));
        });
    }

    [Test]
    public void SameSource_RejectsSecondPathForSameGame()
    {
        Galgame game = new("Example");
        GalgameFolderSource source = CreateSource(@"C:\Library", "Library");
        source.AddGalgame(game, @"C:\Library\Example");

        Assert.That(() => source.AddGalgame(game, @"C:\Library\ExampleCopy"),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void LegacyProperties_ProxyPreferredInstallation()
    {
        Galgame game = new("Example");
        GalgameFolderSource source = CreateSource(@"C:\Library", "Library");
        GalgameAndPath installation = source.AddGalgame(game, @"C:\Library\Example");

#pragma warning disable CS0618
        game.ExePath = @"C:\Library\Example\game.exe";
        game.RunAsAdmin = true;
#pragma warning restore CS0618

        Assert.Multiple(() =>
        {
            Assert.That(installation.LocalConfig!.ExePath, Is.EqualTo(@"C:\Library\Example\game.exe"));
            Assert.That(installation.LocalConfig.RunAsAdmin, Is.True);
        });
    }

    [Test]
    public void UidMatch_ReportsExternalIdAndNameOnlySeparately()
    {
        GalgameUid withId = new() { Name = "A", VndbId = "v1" };
        GalgameUid sameId = new() { Name = "Other", VndbId = "v1" };
        GalgameUid sameName = new() { Name = "A" };

        Assert.Multiple(() =>
        {
            Assert.That(withId.GetMatchKind(sameId), Is.EqualTo(GalgameUidMatchKind.ExternalId));
            Assert.That(withId.GetMatchKind(sameName), Is.EqualTo(GalgameUidMatchKind.NameOnly));
        });
    }

    [Test]
    public void SourceDto_PreservesEntryIdentityAndInstallationConfiguration()
    {
        Galgame game = new("Example");
        GalgameFolderSource source = CreateSource(@"C:\Library", "Library");
        GalgameAndPath installation = source.AddGalgame(game, @"C:\Library\Example",
            localConfig: new LocalInstallationConfig { ProcessName = "example" });

        GalgameAndPathDbDto dto = source.GalgamesDto.Single();

        Assert.Multiple(() =>
        {
            Assert.That(dto.GalgameId, Is.EqualTo(game.Uuid));
            Assert.That(dto.EntryId, Is.EqualTo(installation.EntryId));
            Assert.That(dto.Path, Is.EqualTo(installation.Path));
            Assert.That(dto.LocalConfig?.ProcessName, Is.EqualTo("example"));
        });
    }

    [Test]
    public void LegacyMeta_CanBeConvertedToInstallationConfiguration()
    {
        Galgame legacy = new("Example");
#pragma warning disable CS0618
        legacy.ExePath = @"C:\Old\Example\game.exe";
        legacy.ProcessName = "game";
#pragma warning restore CS0618
        string json = JsonConvert.SerializeObject(legacy);

        Galgame restored = JsonConvert.DeserializeObject<Galgame>(json)!;
        LocalInstallationConfig config = restored.CreateLegacyLocalConfiguration(@"D:\New\Example");

        Assert.Multiple(() =>
        {
            Assert.That(config.ExePath, Is.EqualTo(@"C:\Old\Example\game.exe"));
            Assert.That(config.ProcessName, Is.EqualTo("game"));
        });
    }

    [Test]
    public void LocalInstallationConfig_ClonePreservesLaunchDialogSetting()
    {
        LocalInstallationConfig config = new()
        {
            ExePath = @"D:\Library\Example\game.exe",
            DelayPlayTimeUntilMainWindow = true,
        };

        LocalInstallationConfig clone = config.Clone();

        Assert.Multiple(() =>
        {
            Assert.That(clone, Is.Not.SameAs(config));
            Assert.That(clone.ExePath, Is.EqualTo(config.ExePath));
            Assert.That(clone.DelayPlayTimeUntilMainWindow, Is.True);
        });
    }

    [Test]
    public void VersionTwoMeta_PreservesOnlyItsInstallationConfiguration()
    {
        GameMetaBackup backup = new()
        {
            Version = GameMetaBackup.CurrentVersion,
            Game = new Galgame("Example"),
            Installation = new LocalInstallationConfig
            {
                ExePath = @"D:\Library\Example\game.exe",
                ExeArguments = "--launch",
            },
        };

        GameMetaBackup restored = JsonConvert.DeserializeObject<GameMetaBackup>(
            JsonConvert.SerializeObject(backup))!;

        Assert.Multiple(() =>
        {
            Assert.That(restored.Version, Is.EqualTo(GameMetaBackup.CurrentVersion));
            Assert.That(restored.Game?.Name.Value, Is.EqualTo("Example"));
            Assert.That(restored.Installation?.ExePath, Is.EqualTo(@"D:\Library\Example\game.exe"));
            Assert.That(restored.Installation?.ExeArguments, Is.EqualTo("--launch"));
        });
    }

    [Test]
    public void PluginApi_ExposesMultiInstallationOperationsDirectly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(IPotatoVnApi).IsAssignableFrom(typeof(PluginService.PotatoVnApiHost)), Is.True);
            Assert.That(typeof(IPotatoVnApi).GetMethod(nameof(IPotatoVnApi.GetGameInstallations)), Is.Not.Null);
            Assert.That(typeof(IPotatoVnApi).GetMethod(nameof(IPotatoVnApi.LaunchGameAsync)), Is.Not.Null);
        });
    }

    private static GalgameFolderSource CreateSource(string path, string name) => new()
    {
        Path = path,
        Name = name,
    };
}
