using GalgameManager.Models.BgTasks;

namespace GalgameManager.Test.Models;

[TestFixture]
public class GetHeaderFromRssTaskTest
{
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
}
