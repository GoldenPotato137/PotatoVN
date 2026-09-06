using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
using Moq;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class MixedPhraserTimeoutTest
{
    [Test]
    public async Task GetGalgameInfo_FastSourceSucceeds_SlowSourceTimedOut_ReturnsMergedResult()
    {
        MixedPhraser phraser = CreateMixedPhraser(
            timeoutSeconds: 1,
            CreateDelayedPhraser(RssType.Bangumi, TimeSpan.FromMilliseconds(50), "bgm-1"),
            CreateDelayedPhraser(RssType.Vndb, TimeSpan.FromSeconds(5), "vndb-1"));

        Galgame? result = await phraser.GetGalgameInfo(new Galgame("TestGame"));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Ids[(int)RssType.Bangumi], Is.EqualTo("bgm-1"));
        Assert.That(result.Ids[(int)RssType.Vndb], Is.Null.Or.Empty);
    }

    [Test]
    public async Task GetGalgameInfo_TimeoutZero_WaitsForSlowSource()
    {
        MixedPhraser phraser = CreateMixedPhraser(
            timeoutSeconds: 0,
            CreateDelayedPhraser(RssType.Bangumi, TimeSpan.FromMilliseconds(50), "bgm-1"),
            CreateDelayedPhraser(RssType.Vndb, TimeSpan.FromMilliseconds(400), "vndb-1"));

        Galgame? result = await phraser.GetGalgameInfo(new Galgame("TestGame"));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Ids[(int)RssType.Bangumi], Is.EqualTo("bgm-1"));
        Assert.That(result.Ids[(int)RssType.Vndb], Is.EqualTo("vndb-1"));
    }

    [Test]
    public async Task GetGalgameInfo_AllSourcesTimedOut_ReturnsNull()
    {
        MixedPhraser phraser = CreateMixedPhraser(
            timeoutSeconds: 1,
            CreateDelayedPhraser(RssType.Bangumi, TimeSpan.FromSeconds(5), "bgm-1"),
            CreateDelayedPhraser(RssType.Vndb, TimeSpan.FromSeconds(5), "vndb-1"));

        Galgame? result = await phraser.GetGalgameInfo(new Galgame("TestGame"));

        Assert.That(result, Is.Null);
    }

    private static MixedPhraser CreateMixedPhraser(
        int timeoutSeconds,
        IGalInfoPhraser bangumi,
        IGalInfoPhraser vndb)
    {
        return new MixedPhraser(
            bangumi,
            vndb,
            CreateDelayedPhraser(RssType.Ymgal, TimeSpan.Zero, null),
            CreateDelayedPhraser(RssType.Steam, TimeSpan.Zero, null),
            CreateDelayedPhraser(RssType.Hikarinagi, TimeSpan.Zero, null),
            new MixedPhraserData
            {
                Order = new MixedPhraserOrder().SetToDefault(),
                Enabled = new MixedPhraserEnabled
                {
                    BangumiEnabled = true,
                    VndbEnabled = true,
                    YmgalEnabled = false,
                    SteamEnabled = false,
                    HikarinagiEnabled = false,
                },
                TimeoutSeconds = timeoutSeconds,
            });
    }

    private static IGalInfoPhraser CreateDelayedPhraser(RssType type, TimeSpan delay, string? id)
    {
        Mock<IGalInfoPhraser> mock = new();
        mock.Setup(p => p.GetPhraseType()).Returns(type);
        mock.Setup(p => p.GetGalgameInfo(It.IsAny<Galgame>()))
            .Returns(async (Galgame _) =>
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);
                if (id is null)
                    return null;
                return new Galgame
                {
                    Name = "TestGame",
                    RssType = type,
                    Id = id,
                    Description = $"{type}-desc",
                };
            });
        return mock.Object;
    }
}
