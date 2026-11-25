using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using LiteDB;

namespace GalgameManager.Test.Models;

[TestFixture]
public class GetHeaderFromRssTaskTest
{
    private LiteDatabase _database = null!;
    private FakeLocalSettingsService _settings = null!;

    [SetUp]
    public async Task SetUp()
    {
        _database = new LiteDatabase(new MemoryStream());
        _settings = new FakeLocalSettingsService(_database, Path.GetTempPath());
        await _settings.SaveSettingAsync(KeyValues.UsePvnBanner, true);
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    [TestCase("https://cdn.akamai.steamstatic.com/steam/apps/1/library_hero.jpg", true, false)]
    [TestCase("https://statics.potatovn.net/banner/v17.png", true, false)]
    [TestCase("https://t.vndb.org/sf/02/102.jpg", true, true)]
    [TestCase("https://example.com/header.png", false, false)]
    public void HeaderUrl_ShouldMapToProcessAndCutRule(string url, bool expectedProcess, bool expectedCutBottom)
    {
        Assert.That(GetHeaderFromRssTask.ShouldProcess(url), Is.EqualTo(expectedProcess));
        Assert.That(GetHeaderFromRssTask.ShouldCutBottom(url), Is.EqualTo(expectedCutBottom));
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("-1")]
    public async Task NeedPvnBanner_NonSteamUnlockedGame_ShouldOnlyTryOncePerSession(string? steamId)
    {
        Galgame game = new("game");
        game.Ids[(int)RssType.Steam] = steamId;

        Assert.That(await GetHeaderFromRssTask.NeedPvnBannerAsync(game, _settings), Is.True);
        Assert.That(await GetHeaderFromRssTask.NeedPvnBannerAsync(game, _settings), Is.False);
    }

    [Test]
    public async Task NeedPvnBanner_SettingDisabled_ShouldBeFalse()
    {
        await _settings.SaveSettingAsync(KeyValues.UsePvnBanner, false);
        Galgame game = new("game");

        Assert.That(await GetHeaderFromRssTask.NeedPvnBannerAsync(game, _settings), Is.False);
    }

    [Test]
    public async Task NeedPvnBanner_SteamGame_ShouldBeFalse()
    {
        Galgame game = new("game");
        game.Ids[(int)RssType.Steam] = "1234";

        Assert.That(await GetHeaderFromRssTask.NeedPvnBannerAsync(game, _settings), Is.False);
    }

    [Test]
    public async Task NeedPvnBanner_HeaderLocked_ShouldBeFalse()
    {
        Galgame game = new("game");
        game.HeaderImagePath.IsLock = true;

        Assert.That(await GetHeaderFromRssTask.NeedPvnBannerAsync(game, _settings), Is.False);
    }

    [Test]
    public async Task NeedPvnBanner_AlreadyReplaced_ShouldBeFalse()
    {
        Galgame game = new("game");
        await _settings.SaveSettingAsync(KeyValues.PvnBannerReplacedMap,
            new Dictionary<Guid, bool> { [game.Uuid] = true }, isLarge: true);

        Assert.That(await GetHeaderFromRssTask.NeedPvnBannerAsync(game, _settings), Is.False);
    }
}
